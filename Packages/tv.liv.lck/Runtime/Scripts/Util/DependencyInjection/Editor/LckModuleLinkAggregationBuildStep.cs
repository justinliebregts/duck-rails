#if !LCK_SKIP_LINK_BUILD_STEP
using UnityEditor;

namespace Liv.Lck.DependencyInjection
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using UnityEditor.Build;
    using UnityEditor.Build.Reporting;
    using UnityEditor.PackageManager;
    using UnityEditor.PackageManager.Requests;
    using UnityEngine;

    /// <summary>
    /// Build steps to prevent stripping of optional LCK packages.
    ///
    /// Discovers custom linker definition files ('LckModuleLink.xml') within all installed packages and
    /// merges them into a single 'link.xml' in a temporary directory during the build process.
    /// This ensures that code stripping rules defined in LCKs module packages are correctly applied.
    /// 
    /// Based upon the recommendation in https://discussions.unity.com/t/while-custom-package-need-a-link-xml/754406
    /// </summary>
    public class LckModuleLinkAggregationBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const int MAX_CLEANUP_RETRIES = 20; 
        
        private static readonly string CustomLinkerDefinitionFile = "LckModuleLink.xml";
        private static readonly string TempLinkFolder = Path.Combine(Application.dataPath, "LCKBuildLinker").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        private static readonly string TempLinkFolderMeta = TempLinkFolder + ".meta";
        private static readonly string LinkFilePath = Path.Combine(TempLinkFolder, "link.xml");

        private static int _retryCount = 0;
        private static double _startTime = 0;
        
        public int callbackOrder => 10;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("LCK: Starting pre-build step to aggregate linker definitions...");
            try
            {
                AggregateLinkerDefinitions();
                Debug.Log("LCK: Successfully aggregated linker definitions.");
            }
            catch (Exception ex)
            {
                throw new BuildFailedException($"LCK: Failed to aggregate linker definitions. Halting build. Error: {ex}");
            }
        }
        
        public void OnPostprocessBuild(BuildReport report)
        {
            _retryCount = 0;
            _startTime = EditorApplication.timeSinceStartup;
            EditorApplication.delayCall += AttemptForcedDelete;
        }

        private static void AttemptForcedDelete()
        {
            EditorApplication.delayCall -= AttemptForcedDelete;

            _retryCount++;
            if (_retryCount > MAX_CLEANUP_RETRIES)
            {
                Debug.LogError($"LCK: Build cleanup task failed after {MAX_CLEANUP_RETRIES} retries. The folder '{TempLinkFolder}' may still exist.");
                return;
            }

            if (!Directory.Exists(TempLinkFolder))
            {
                return;
            }
            
            bool success = false;
            try
            {
                Directory.Delete(TempLinkFolder, true);
                
                if(File.Exists(TempLinkFolderMeta))
                {
                    File.Delete(TempLinkFolderMeta);
                }
                
                success = true; 
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"LCK: Cleanup attempt #{_retryCount} failed: {ex.Message}. Retrying...");
                success = false;
            }

            if (success)
            {
                Debug.Log($"LCK: Successfully cleaned up temporary build assets after {_retryCount} attempt(s).");
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            }
            else
            {
                EditorApplication.delayCall += AttemptForcedDelete;
            }
        }
        
        private void AggregateLinkerDefinitions()
        {
            var packageList = GetInstalledPackagesSync();
            if (packageList == null)
            {
                Debug.LogWarning("LCK: Could not retrieve package list. Skipping linker aggregation.");
                return;
            }

            var linkerFiles = FindLinkerDefinitionFiles(packageList);

            if (!linkerFiles.Any())
            {
                Debug.Log("LCK: No 'LckModuleLink.xml' files found. Nothing to merge.");
                return;
            }
            
            var mergedDocument = MergeXmlDocuments(linkerFiles);

            Directory.CreateDirectory(TempLinkFolder); 
            
            mergedDocument.Save(LinkFilePath);
        }

        private PackageCollection GetInstalledPackagesSync()
        {
            ListRequest request = Client.List(true); 
            while (!request.IsCompleted)
            {
            }

            if (request.Status == StatusCode.Success)
            {
                return request.Result;
            }
          
            return null;
        }

        private IEnumerable<string> FindLinkerDefinitionFiles(IEnumerable<PackageInfo> packages)
        {
            return packages
                .Select(p => p.resolvedPath)
                .SelectMany(path => Directory.EnumerateFiles(path, CustomLinkerDefinitionFile, SearchOption.AllDirectories));
        }

        private XDocument MergeXmlDocuments(IEnumerable<string> filePaths)
        {
            var documents = filePaths.Select(XDocument.Load).ToList();
            
            if (!documents.Any()) return new XDocument(new XElement("linker"));

            XDocument baseDoc = documents.First();
            XElement root = baseDoc.Root;

            foreach (var doc in documents.Skip(1))
            {
                root.Add(doc.Root?.Elements());
            }

            return baseDoc;
        }
    }
}
#endif