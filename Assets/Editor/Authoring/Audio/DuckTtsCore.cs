using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

/// <summary>Bundled Kokoro-82M CPU inference. No Unity or network calls.
/// Authored lines use context-aware Misaki phonemes prepared during development;
/// new text uses the local word lexicon. Run on one worker at a time.</summary>
public sealed class DuckTtsCore : IDisposable
{
    readonly InferenceSession session;
    readonly Dictionary<string, string> lexicon = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<char, long> symbols = new Dictionary<char, long>();
    readonly Dictionary<string, string> phrases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    readonly string folder;
    readonly Dictionary<string, float[]> styles = new Dictionary<string, float[]>();
    public const int SampleRate = 24000;
    public const string DefaultVoice = "am_puck";
    public const string AssetFolder = "DuckSpeech/Kokoro";
    public static readonly string[] Voices = { DefaultVoice, "am_fenrir" };
    public static readonly string[] BundledFiles = { "kokoro.onnx", "am_puck.bytes", "am_fenrir.bytes", "vocab.tsv", "phrases.tsv", "lexicon.tsv" };
    static readonly string[] Digits = { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
    static readonly Regex Words = new Regex(@"[A-Za-z]+(?:'[A-Za-z]+)*|[0-9]|[.,!?;:]", RegexOptions.Compiled);

    public DuckTtsCore(string directory)
    {
        folder = Path.GetFullPath(directory);
        ValidateBundle(folder);
        ReadTable(Path.Combine(folder, "lexicon.tsv"), lexicon);
        ReadTable(Path.Combine(folder, "phrases.tsv"), phrases);
        foreach (string line in File.ReadLines(Path.Combine(folder, "vocab.tsv")))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 2 || fields[0].Length != 1) throw new InvalidDataException("Invalid Kokoro vocabulary.");
            symbols.Add(fields[0][0], long.Parse(fields[1], CultureInfo.InvariantCulture));
        }
        using (var options = new SessionOptions())
        {
            options.IntraOpNumThreads = 2;
            options.InterOpNumThreads = 1;
            options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            session = new InferenceSession(Path.Combine(folder, "kokoro.onnx"), options);
        }
    }

    public static string HashFile(string path)
    {
        using (var input = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
    }

    public static void ValidateBundle(string directory)
    {
        var verified = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(Path.Combine(directory, "checksums.tsv")))
        {
            string[] fields = line.Split('\t');
            if (fields.Length != 3 || !BundledFiles.Contains(fields[0]) || !verified.Add(fields[0]))
                throw new InvalidDataException("Invalid Kokoro bundle manifest.");
            string path = Path.Combine(directory, fields[0]);
            if (!File.Exists(path) || new FileInfo(path).Length != long.Parse(fields[1], CultureInfo.InvariantCulture)
                || HashFile(path) != fields[2]) throw new InvalidDataException("Bundled voice file is missing or damaged: " + fields[0]);
        }
        if (verified.Count != BundledFiles.Length) throw new InvalidDataException("Incomplete Kokoro bundle.");
    }

    static void ReadTable(string path, Dictionary<string, string> target)
    {
        foreach (string line in File.ReadLines(path))
        {
            int split = line.IndexOf('\t');
            if (split > 0) target[line.Substring(0, split)] = line.Substring(split + 1);
        }
    }

    public static string ResolveVoice(string voice) => Voices.Contains(voice) ? voice : DefaultVoice;

    public string Phonemize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Trim().Replace('’', '\'');
        if (phrases.TryGetValue(text, out string prepared)) return prepared;
        var output = new List<string>();
        foreach (Match match in Words.Matches(text.Replace('’', '\'')))
        {
            string word = match.Value.ToLowerInvariant();
            if (word.Length == 1 && char.IsDigit(word[0])) word = Digits[word[0] - '0'];
            if (word.Length == 1 && !char.IsLetter(word[0]))
            {
                if (output.Count > 0) output[output.Count - 1] += word;
                continue;
            }
            output.Add(Pronunciation(word));
        }
        // Preserve attached stress marks and Kokoro's compact diphthongs.
        return string.Join(" ", output);
    }

    string Pronunciation(string word)
    {
        if (lexicon.TryGetValue(word, out string value)) return value;
        if (word.EndsWith("s") && lexicon.TryGetValue(word.Substring(0, word.Length - 1), out value))
            return value + ("ptkfθ".IndexOf(value[value.Length - 1]) >= 0 ? "s" : "z");
        if (word.EndsWith("ing") && word.Length > 4)
        {
            string stem = word.Substring(0, word.Length - 3);
            if (lexicon.TryGetValue(stem, out value) || lexicon.TryGetValue(stem + "e", out value)) return value + "ɪŋ";
        }
        // Unknown names are spelled rather than inventing a pronunciation.
        var spelling = new List<string>();
        foreach (char letter in word)
            if (lexicon.TryGetValue(letter.ToString(), out value)) spelling.Add(value);
        return string.Join(" ", spelling);
    }

    public float[] Generate(string text, string voice = DefaultVoice, float speed = 1f)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();
        if (text.Length > 280) throw new ArgumentException("Duck speech is limited to 280 characters.", nameof(text));
        if (float.IsNaN(speed) || float.IsInfinity(speed)) throw new ArgumentException("Speech speed must be finite.", nameof(speed));
        voice = ResolveVoice(voice);
        if (!styles.TryGetValue(voice, out float[] allStyles))
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(folder, voice + ".bytes"));
            if (bytes.Length != 510 * 256 * sizeof(float)) throw new InvalidDataException("Invalid Kokoro voice style.");
            allStyles = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, allStyles, 0, bytes.Length);
            styles[voice] = allStyles;
        }
        var tokens = new List<long>();
        foreach (char c in Phonemize(text))
        {
            if (!symbols.TryGetValue(c, out long id)) throw new InvalidDataException("Unsupported Kokoro phoneme: " + c);
            tokens.Add(id);
        }
        if (tokens.Count == 0) return Array.Empty<float>();
        if (tokens.Count > 510) throw new InvalidDataException("Speech exceeds Kokoro's phoneme context.");
        float[] style = new float[256];
        // Reference row is indexed by phoneme-token count, before boundary pads.
        Array.Copy(allStyles, Math.Min(tokens.Count, 509) * 256, style, 0, 256);
        tokens.Insert(0, 0);
        tokens.Add(0);
        var inputs = new[] {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(tokens.ToArray(), new[] { 1, tokens.Count })),
            NamedOnnxValue.CreateFromTensor("style", new DenseTensor<float>(style, new[] { 1, 256 })),
            NamedOnnxValue.CreateFromTensor("speed", new DenseTensor<float>(new[] { Math.Max(0.6f, Math.Min(1.4f, speed)) }, new[] { 1 }))
        };
        using (var result = session.Run(inputs))
        {
            float[] audio = result.First().AsEnumerable<float>().ToArray();
            if (audio.Length < 2400 || audio.Length > SampleRate * 45) throw new InvalidDataException("Speech output has an invalid duration.");
            for (int i = 0; i < audio.Length; i++)
            {
                if (float.IsNaN(audio[i]) || float.IsInfinity(audio[i])) throw new InvalidDataException("Non-finite speech output.");
                audio[i] = Math.Max(-1, Math.Min(1, audio[i]));
            }
            // Preserve Kokoro's natural pacing and full sentence ending.
            return audio;
        }
    }

    public void Dispose() => session.Dispose();
}
