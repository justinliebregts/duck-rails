using System;
using System.Globalization;
using System.Text.RegularExpressions;

// Authored rules remain ordered: earlier matching topics retain priority.
internal static class DuckIntentMatcher
{
    static readonly string[] WordsGoHome = { "go home", "go back", "go away", "stop follow", "stop following", "leave me alone", "give me space", "don't follow", "do not follow" };
    static readonly string[] WordsShutUp = { "shut up", "be quiet", "stop talking", "quiet please", "keep quiet", "stay quiet" };
    static readonly string[] WordsWait = { "wait", "stay here" };
    static readonly string[] WordsHelicopter = { "helicopter", "helicopters", "chopper", "supplies", "survival gear", "gear" };
    static readonly string[] WordsTakeMe = { "take me", "take us", "show me", "show us", "lead me", "lead us", "where is", "where's",
                "where the", "how do i get", "how do we get", "which way" };
    static readonly string[] WordsFollow = { "follow", "start following", "resume following", "stay with me", "stay with us",
            "stick with me", "stick with us", "come here", "come back", "come with", "come along" };
    static readonly string[] WordsCanYouHearMe = { "can you hear me", "do you hear me", "did you hear me", "are you listening",
            "can you hear us", "do you hear us", "did you hear that", "you there", "are you deaf",
            "mic check", "microphone check", "testing testing", "say something", "respond to me" };
    static readonly string[] WordsWhoAreYou = { "who are you", "who is duck", "what are you", "your name", "tell me about yourself",
            "introduce yourself", "why are you here", "why do you live here", "what are you doing here",
            "where do you live", "where are you from", "where do you come from", "why do you exist",
            "are you real", "what is your purpose", "what's your purpose", "what is your story", "what's your story" };
    static readonly string[] WordsClimb = { "climb", "climbing", "clamber", "get on the roof", "get onto the roof" };
    static readonly string[] WordsJump = { "jump", "jumping" };
    static readonly string[] WordsSkin = { "skin", "skins", "avatar", "avatars", "outfit", "outfits", "costume", "costumes",
            "your appearance", "your looks", "your feathers", "your face", "you look ugly", "you're ugly",
            "you are ugly", "you look cute", "you're cute", "you are cute", "you look handsome" };
    static readonly string[] WordsCharacter = { "character", "characters" };
    static readonly string[] WordsYour = { "your", "duck", "player", "players", "ugly", "cute", "looks", "look" };
    static readonly string[] WordsDrink = { "drink", "drinks", "beverage", "beverages", "tea", "coffee", "juice", "soda", "lemonade", "hot chocolate", "milkshake", "milkshakes" };
    static readonly string[] WordsFavorite = { "favorite", "favorites", "favourite", "favourites", "like", "likes", "love", "loves",
                "enjoy", "enjoyed", "prefer", "best", "recommend", "grew up", "growing up", "childhood",
                "what do you drink", "what did you drink", "what would you drink" };
    static readonly string[] WordsMusic = { "music", "song", "songs", "tune", "tunes", "band", "bands", "singer", "singers" };
    static readonly string[] WordsFavorite2 = { "favorite", "favorites", "favourite", "favourites", "like", "likes", "love", "loves",
                "enjoy", "enjoyed", "prefer", "best", "recommend", "listen", "listened", "listening",
                "grew up", "growing up", "childhood" };
    static readonly string[] WordsThunder = { "thunder", "thunderstorm", "thunderstorms", "lightning", "storm", "storms", "power outage",
            "power cut", "power cuts", "blackout", "blackouts", "power went out", "power is out", "lights went out", "no electricity" };
    static readonly string[] WordsCrow = { "crow", "crows", "caw", "cawing", "caw caw", "raven", "ravens" };
    static readonly string[] WordsBanging = { "banging", "pounding", "clanging", "clanking", "metal banging", "factory noise", "factory noises", "factory sound", "factory sounds" };
    static readonly string[] WordsFactory = { "factory", "factories", "machinery" };
    static readonly string[] WordsLoud = { "loud", "noise", "noisy", "bang", "bangs", "racket", "knocking" };
    static readonly string[] WordsMilitary = { "military", "army", "soldier", "soldiers", "troops", "gun", "guns", "gunfire", "gunshots", "gunshot",
            "shooting", "rifle", "rifles", "machine gun", "machine guns", "artillery", "distant shots" };
    static readonly string[] WordsCave = { "cave", "caves", "cavern", "caverns", "the mine", "old mine", "abandoned mine", "mineshaft", "mines", "mining cave", "mine cart", "mining tunnel" };
    static readonly string[] WordsMovie = { "movie", "movies", "film", "films" };
    static readonly string[] WordsFavorite3 = { "favorite", "favorites", "favourite", "favourites", "like", "likes", "love", "loves",
                "enjoy", "enjoyed", "prefer", "best", "recommend", "watched", "seen", "do you watch", "did you watch" };
    static readonly string[] WordsCinema = { "cinema", "cinemas", "movie theater", "movie theatre", "movie house", "movies", "theater", "theatre" };
    static readonly string[] WordsApartment = { "apartment", "apartments", "apartment block", "apartment building", "flats" };
    static readonly string[] WordsWarehouse = { "warehouse", "warehouses", "storage depot", "loading dock" };
    static readonly string[] WordsSubway = { "subway", "subways", "subway station", "metro", "underground station", "train station", "train platform" };
    static readonly string[] WordsSheriff = { "sheriff", "sheriffs", "cop", "cops", "police", "police station", "sheriff office", "sheriff's office", "holding cell" };
    static readonly string[] WordsGasStation = { "gas station", "gas stations", "petrol station", "filling station", "fuel station", "gas pumps", "friend's uncle", "friends uncle" };
    static readonly string[] WordsChurch = { "church", "churches", "chapel", "bell", "bells", "bell tower", "pews" };
    static readonly string[] WordsBarn = { "barn", "barns", "farm", "farmhouse", "farm house", "farm animals", "barn animals", "hayloft", "hay loft", "chicken coop" };
    static readonly string[] WordsCreek = { "creek", "bridge", "bridges", "aunt", "river", "riverbank", "river bank", "swimming", "swim", "paddling" };
    static readonly string[] WordsWoods = { "woods", "forest", "woodland", "hike", "hiking", "trees", "camping", "campfire", "cabin", "cabins", "s'mores", "smores", "marshmallow", "marshmallows", "marsh mellows" };
    static readonly string[] WordsRambro = { "rambro", "ram bro", "rambo" };
    static readonly string[] WordsHero = { "hero", "childhood", "kid", "grew up" };
    static readonly string[] WordsHelicopter2 = { "helicopter", "helicopters", "chopper" };
    static readonly string[] WordsRemote = { "remote", "childhood", "kid", "used to" };
    static readonly string[] WordsSchoolBus = { "school bus", "schoolbus", "bus", "buses", "bus stop", "bus ride" };
    static readonly string[] WordsTruck = { "truck", "trucks", "pickup truck", "delivery truck", "lorry", "lorries" };
    static readonly string[] WordsSchool = { "school", "classroom", "classrooms", "teacher", "homework", "schoolhouse", "school house", "playground", "recess", "detention" };
    static readonly string[] WordsFactory2 = { "factory", "factories", "old job", "used to work", "where did you work", "assembly line" };
    static readonly string[] WordsStore = { "store", "shop", "shopping", "groceries", "general store", "grocery store", "supermarket" };
    static readonly string[] WordsDiner = { "diner", "restaurant", "cafe", "coffee shop" };
    static readonly string[] WordsWaterTower = { "water tower", "watertower", "water tank" };
    static readonly string[] WordsHelipad = { "helipad", "heli pad", "landing pad", "helicopter pad", "landing zone" };
    static readonly string[] WordsCity = { "city", "cities" };
    static readonly string[] WordsTown = { "town", "quackville", "hometown", "town square", "main street" };
    static readonly string[] WordsCreepy = { "creepy", "imposter", "impostor", "serial killer", "forever buddy", "watching people sleep",
            "watch me sleep", "smell of flesh", "secret admirer", "secretly in love" };
    static readonly string[] WordsFriend = { "friend", "friends", "friendship", "buddy", "buddies", "bestie", "befriend" };
    static readonly string[] WordsHelicopter3 = { "helicopter", "helicopters", "chopper", "rotor", "rotors" };
    static readonly string[] WordsNailBat = { "nail bat", "nail bats", "nailed bat", "baseball bat", "bat", "bats" };
    static readonly string[] WordsFryPan = { "fry pan", "frying pan", "frypan", "pan", "pans", "skillet" };
    static readonly string[] WordsNightVision = { "night vision", "nightvision", "goggles" };
    static readonly string[] WordsFlashlight = { "flashlight", "flashlights", "flash light", "torch" };
    static readonly string[] WordsHelmet = { "helmet", "helmets", "hard hat" };
    static readonly string[] WordsWalkieTalkie = { "walkie talkie", "walkie", "walkies", "radio", "radios" };
    static readonly string[] WordsRambro2 = { "rambro", "ram bro", "rambo" };
    static readonly string[] WordsNinja = { "ninja", "ninja knife" };
    static readonly string[] WordsFood = { "food", "foods", "pizza", "pizzas", "burger", "burgers", "hamburger", "hamburgers",
            "cheeseburger", "cheeseburgers", "rice", "fries", "french fries", "chips", "potato", "potatoes",
            "pasta", "noodles", "spaghetti", "bread", "toast", "sandwich", "sandwiches", "taco", "tacos",
            "burrito", "burritos", "soup", "salad", "steak", "meat", "chicken", "beef", "pork", "bacon",
            "sausage", "sausages", "fish", "seafood", "sushi", "egg", "eggs", "cheese", "vegetables", "fruit",
            "apple", "apples", "banana", "bananas", "cake", "cookies", "cookie", "ice cream", "chocolate",
            "candy", "dessert", "snack", "snacks", "breakfast", "lunch", "dinner", "supper", "meal", "meals",
            "hungry", "hunger", "eat", "eating", "ate", "cooking", "cook", "recipe", "recipes",
            "delicious", "tasty", "flavor", "flavour", "duck flesh", "duck meat" };
    static readonly string[] WordsGold = { "gold", "coin", "coins", "money", "deposit" };
    static readonly string[] WordsBag = { "bag", "bags", "backpack", "backpacks", "carry", "inventory", "storage" };
    static readonly string[] WordsSupplies = { "supplies", "supply", "survival gear", "gear" };
    static readonly string[] WordsSchool2 = { "school", "classroom", "teacher", "homework" };
    static readonly string[] WordsFactory3 = { "factory", "factories", "old job", "used to work", "where did you work" };
    static readonly string[] WordsRiver = { "river", "swimming", "swim", "paddling" };
    static readonly string[] WordsWoods2 = { "woods", "forest", "hike", "hiking", "trees" };
    static readonly string[] WordsCity2 = { "city", "cities", "noisy", "noise" };
    static readonly string[] WordsTown2 = { "town", "quackville", "hometown", "grew up", "grow up", "childhood", "quiet", "where are you from" };
    static readonly string[] WordsWeapon = { "weapon", "weapons", "unlock", "unlocking", "buy", "purchase", "price", "knife", "knives" };
    static readonly string[] WordsStore2 = { "store", "shop", "shopping", "groceries" };
    static readonly string[] WordsRich = { "rich", "wealth", "fortune", "treasure" };
    static readonly string[] WordsDied = { "died", "dead", "respawn", "killed" };
    static readonly string[] WordsStayingAlive = { "staying alive", "stay alive", "survive", "survival", "safe", "safety", "tips" };
    static readonly string[] WordsZombie = { "zombie", "zombies", "danger", "behind", "run" };
    static readonly string[] WordsClimb2 = { "climb", "climbing", "roof", "rooftop", "stairs", "vehicle", "car", "truck", "bus" };
    static readonly string[] WordsJump2 = { "jump", "gap" };
    static readonly string[] WordsAgain = { "again", "repeat", "what was that", "tell me more", "go on", "yes please" };
    static readonly string[] WordsJoke = { "joke", "funny", "laugh" };
    static readonly string[] WordsThank = { "thank", "thanks", "good duck", "nice", "love you" };
    static readonly string[] WordsStupid = { "stupid", "hate", "useless", "bad duck" };
    static readonly string[] WordsWhoAre = { "who are", "your name", "what are you" };
    static readonly string[] WordsHowAre = { "how are", "happy", "feeling" };
    static readonly string[] WordsBye = { "bye", "goodbye", "see you" };
    static readonly string[] WordsHello = { "hello", "hey", "hi", "howdy" };

    public static string Intent(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";
        string t = NormalizeWhitespace.Replace(text.ToLowerInvariant(), " ");
        if (Has(t, WordsGoHome)) return "dismiss";
        if (Has(t, WordsShutUp)) return "quiet";
        if (Has(t, WordsWait)) return "wait";
        // Direction requests are verbal only; do not change the companion's destination.
        if (Has(t, WordsHelicopter)
            && Has(t, WordsTakeMe)) return "helicopter_directions";
        if (Has(t, WordsFollow)) return "follow";
        if (Has(t, WordsCanYouHearMe)) return "acknowledge";
        if (Has(t, WordsWhoAreYou)) return "identity";
        // Whole laugh tokens only: never match words such as "what" or "perhaps".
        if (Laughter.IsMatch(t)) return "laughter";
        // Explicit movement questions stay useful even when a landmark is named.
        if (Has(t, WordsClimb)) return "climbing";
        if (Has(t, WordsJump)) return "jumping";
        if (IsJealousAppearanceComparison(t)) return "appearance_jealous";
        // Multiplayer appearance talk includes compliments and insults. Keep
        // explicit commands above this, and character backstory in its own topic.
        if (Has(t, WordsSkin)
            || (Has(t, WordsCharacter) && Has(t, WordsYour)))
            return "appearance";
        // Personal drink preferences precede generic food and childhood-place topics.
        if (Has(t, WordsDrink)
            && Has(t, WordsFavorite)) return "favorite_drinks";
        // Personal music questions precede place memories and radio equipment advice.
        if (Has(t, WordsMusic)
            && Has(t, WordsFavorite2)) return "favorite_music";
        // Specific ambient subjects override general place/food memories, without
        // claiming a sound is playing now. Commands and movement requests stay first.
        if (Has(t, WordsThunder)) return "thunder";
        if (Has(t, WordsCrow)) return "crows";
        if (Has(t, WordsBanging)
            || (Has(t, WordsFactory) && Has(t, WordsLoud))) return "factory_banging";
        if (Has(t, WordsMilitary)) return "military_sounds";
        // Named memories precede generic equipment words (cave bats, bags of
        // marshmallows, or a childhood hero must not become inventory advice).
        if (Has(t, WordsCave)) return "caves";
        // Film preferences get their own answer rather than generic cinema memories.
        if (Has(t, WordsMovie)
            && Has(t, WordsFavorite3)) return "favorite_movies";
        if (Has(t, WordsCinema)) return "cinema";
        if (Has(t, WordsApartment)) return "apartments";
        if (Has(t, WordsWarehouse)) return "warehouse";
        if (Has(t, WordsSubway)) return "subway";
        if (Has(t, WordsSheriff)) return "sheriff";
        if (Has(t, WordsGasStation)) return "gas_station";
        if (Has(t, WordsChurch)) return "church";
        if (Has(t, WordsBarn)) return "barn";
        if (Has(t, WordsCreek)) return "river";
        if (Has(t, WordsWoods)) return "woods";
        if (Has(t, WordsRambro) && Has(t, WordsHero)) return "rambro_memories";
        if (Has(t, WordsHelicopter2) && Has(t, WordsRemote)) return "helicopter_memories";
        if (Has(t, WordsSchoolBus)) return "bus_memories";
        if (Has(t, WordsTruck)) return "truck_memories";
        if (Has(t, WordsSchool)) return "school";
        if (Has(t, WordsFactory2)) return "factory";
        if (Has(t, WordsStore)) return "store";
        if (Has(t, WordsDiner)) return "diner";
        if (Has(t, WordsWaterTower)) return "water_tower";
        if (Has(t, WordsHelipad)) return "helipad_memories";
        if (Has(t, WordsCity)) return "city";
        if (Has(t, WordsTown)) return "town";
        if (Has(t, WordsCreepy)) return "creepy";
        if (Has(t, WordsFriend)) return "friendship";
        // Specific subjects precede generic combat, shopping and joke words.
        if (Has(t, WordsHelicopter3)) return "helicopter";
        if (Has(t, WordsNailBat)) return "nail_bat";
        if (Has(t, WordsFryPan)) return "fry_pan";
        if (Has(t, WordsNightVision)) return "night_vision";
        if (Has(t, WordsFlashlight)) return "flashlight";
        if (Has(t, WordsHelmet)) return "helmet";
        if (Has(t, WordsWalkieTalkie)) return "walkie_talkie";
        if (Has(t, WordsRambro2)) return "rambro_knife";
        if (Has(t, WordsNinja)) return "ninja_knife";
        // Everyday food beats generic shopping, money and joke words. Explicit
        // equipment and childhood places above retain their contextual answers.
        if (Has(t, WordsFood)) return "creepy_food";
        if (Has(t, WordsGold)) return "gold";
        if (Has(t, WordsBag)) return "bags";
        // General supply questions point to the helicopter; named items and places win above.
        if (Has(t, WordsSupplies)) return "helicopter";
        if (Has(t, WordsSchool2)) return "school";
        if (Has(t, WordsFactory3)) return "factory";
        if (Has(t, WordsRiver)) return "river";
        if (Has(t, WordsWoods2)) return "woods";
        if (Has(t, WordsCity2)) return "city";
        if (Has(t, WordsTown2)) return "town";
        if (Has(t, WordsWeapon)) return "weapons";
        if (Has(t, WordsStore2)) return "store";
        if (Has(t, WordsRich)) return "gold";
        if (Has(t, WordsDied)) return "death";
        if (Has(t, WordsStayingAlive)) return "survival";
        if (Has(t, WordsZombie)) return "danger";
        if (Has(t, WordsClimb2)) return "climbing";
        if (Has(t, WordsJump2)) return "jumping";
        if (Has(t, WordsAgain)) return "repeat";
        if (Has(t, WordsJoke)) return "jokes";
        if (Has(t, WordsThank)) return "thanks";
        if (Has(t, WordsStupid)) return "kindness";
        if (Has(t, WordsWhoAre)) return "identity";
        if (Has(t, WordsHowAre)) return "mood";
        if (Has(t, WordsBye)) return "goodbye";
        if (Has(t, WordsHello)) return "greeting";
        return "unknown";
    }


    static readonly Regex NormalizeWhitespace = new Regex(@"[-_\s]+");
    static readonly Regex WakeWord = new Regex(@"\bduck\b", RegexOptions.IgnoreCase);
    static readonly Regex Laughter = new Regex(@"\b(?:(?:ha){1,8}|(?:he){2,8}|heh|hah|lol|lmao)\b");
    const string Comparison = @"(?:cuter|prettier|more handsome|more attractive|better looking|better(?: looking)? (?:skin|avatar|outfit|costume))";
    static readonly Regex NegatedComparison = new Regex(@"\b(?:not|isn't|aren't|isnt|arent)\s+(?:\w+\s+){0,2}" + Comparison + @"\s+than\s+(?:you|duck)\b");
    static readonly Regex DirectComparison = new Regex(@"\b" + Comparison + @"\s+than\s+(?:you|duck|your (?:skin|avatar|outfit|costume))\b");
    static readonly Regex OtherComparison = new Regex(@"\b(?:his|her|their|that player's|that players) (?:skin|avatar|outfit|costume) (?:is|looks) (?:better|nicer|cuter) than (?:yours|duck's|ducks)\b");

    public static bool HasWakeWord(string text) => WakeWord.IsMatch(text);

    static bool IsJealousAppearanceComparison(string text) => !NegatedComparison.IsMatch(text)
        && (DirectComparison.IsMatch(text) || OtherComparison.IsMatch(text));

    static bool Has(string text, string[] words)
    {
        foreach (string word in words)
            if (ContainsWholePhrase(text, word)) return true;
        return false;
    }

    // Literal phrases keep the former regex's boundary semantics, including combining
    // marks and join controls. Text is already lowercased; no substring allocations.
    public static bool ContainsWholePhrase(string text, string phrase)
    {
        int from = 0;
        while (from <= text.Length - phrase.Length)
        {
            int at = text.IndexOf(phrase, from, StringComparison.Ordinal);
            if (at < 0) return false;
            int end = at + phrase.Length;
            if ((at == 0 || !IsBoundaryWordChar(text[at - 1])) &&
                (end == text.Length || !IsBoundaryWordChar(text[end]))) return true;
            from = at + 1;
        }
        return false;
    }

    static bool IsBoundaryWordChar(char c)
    {
        if (c <= 127) return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
            || (c >= '0' && c <= '9') || c == '_';
        if (c == '\u200c' || c == '\u200d') return true;
        switch (char.GetUnicodeCategory(c))
        {
            case UnicodeCategory.UppercaseLetter:
            case UnicodeCategory.LowercaseLetter:
            case UnicodeCategory.TitlecaseLetter:
            case UnicodeCategory.ModifierLetter:
            case UnicodeCategory.OtherLetter:
            case UnicodeCategory.NonSpacingMark:
            case UnicodeCategory.DecimalDigitNumber:
            case UnicodeCategory.ConnectorPunctuation: return true;
            default: return false;
        }
    }
}
