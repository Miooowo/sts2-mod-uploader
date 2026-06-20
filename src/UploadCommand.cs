using System.Text.Json;
using System.Text.RegularExpressions;
using Steamworks;

namespace ModUploader;

public static class UploadCommand
{
    private static readonly AppId_t _sts2AppId = new(2868840);
    
    public static async Task<int> UploadWorkspace(DirectoryInfo workspaceDirectory, ulong? itemIdArg)
    {
        if (!workspaceDirectory.Exists)
        {
            Log.Error($"No directory at {workspaceDirectory}!");
            return 1;
        }
        
        // First, do some validation of what is in the directory.
        FileInfo imageFileInfo = new FileInfo(Path.Combine(workspaceDirectory.FullName, "image.png"));
        if (!imageFileInfo.Exists)
        {
            Log.Error("There is no file named image.png in the workspace!");
            return 1;
        }

        DirectoryInfo contentDirectoryInfo = new DirectoryInfo(Path.Combine(workspaceDirectory.FullName, "content"));
        if (!contentDirectoryInfo.Exists)
        {
            Log.Error("There is no 'content' directory inside the workspace!");
            return 1;
        }

        FileInfo configJsonInfo = new FileInfo(Path.Combine(workspaceDirectory.FullName, "workshop.json"));
        if (!configJsonInfo.Exists)
        {
            Log.Error("There is no file named workshop.json in the workspace!");
            return 1;
        }

        ModConfig? modConfig;
        
        try
        {
            await using FileStream configJsonStream = configJsonInfo.Open(FileMode.Open);
            modConfig = await JsonSerializer.DeserializeAsync(configJsonStream, SourceGenerationContext.Default.ModConfig);
        }
        catch (JsonException)
        {
            Log.Error("Exception thrown while parsing the workshop config! Double-check that the format is correct.");
            throw;
        }

        if (modConfig == null)
        {
            Log.Error("Tried to parse workshop.json, but it returned null!");
            return 1;
        }

        if (modConfig.visibility != null && VisibiltyFromString(modConfig.visibility) == null)
        {
            Log.Error($"Invalid visibility '{modConfig.visibility}' in workshop.json! Should be: private, public, unlisted, or friends_only");
            return 1;
        }

        Dictionary<string, string>? localizedDescriptions = BuildLocalizedDescriptions(
            workspaceDirectory,
            contentDirectoryInfo,
            modConfig.localizedDescriptions);

        ulong? modIdTxt = null;
        
        FileInfo modIdFile = new(Path.Combine(workspaceDirectory.FullName, "mod_id.txt"));
        if (modIdFile.Exists)
        {
            await using FileStream modIdStream = modIdFile.OpenRead();
            using StreamReader reader = new(modIdStream);
            string modIdStr = (await reader.ReadToEndAsync()).Trim();

            if (!ulong.TryParse(modIdStr, out ulong modId))
            {
                Log.Error("Tried to read mod ID from mod_id.txt, but the text could not be parsed as a mod ID!");
                return 1;
            }

            modIdTxt = modId;
        }

        // Validation is all done. Start the upload process.
        if (!Program.InitializeSteam())
        {
            return 1;
        }
        
        Log.Info("=================");
        Log.Info($"By submitting '{modConfig.title}' to the workshop,\n" +
                 $"you agree to the Steam Workshop terms of service:\n" +
                 $"https://steamcommunity.com/sharedfiles/workshoplegalagreement");
        Log.Info("=================");

        PublishedFileId_t workshopItem;
        
        Log.Info($"Logged in as user '{SteamFriends.GetPersonaName()}'.");

        if (itemIdArg != null)
        {
            Log.Info($"Using workshop item ID {itemIdArg.Value} passed in via command line");
            workshopItem = new PublishedFileId_t(itemIdArg.Value);

            bool exists = await DoesWorkshopItemExist(workshopItem);
            if (!exists)
            {
                Log.Error($"Tried to upload to workshop item with ID {itemIdArg.Value} passed via command line, but it doesn't exist!");
                return 1;
            }
        }
        else if (modIdTxt != null)
        {
            Log.Info($"Using workshop item ID {modIdTxt.Value} from mod_id.txt");
            workshopItem = new PublishedFileId_t(modIdTxt.Value);

            bool exists = await DoesWorkshopItemExist(workshopItem);
            if (!exists)
            {
                Log.Error($"Tried to upload to workshop item with ID {modIdTxt.Value} but it doesn't exist! If you wish to upload a new item, delete 'mod_id.txt' from your mod directory.");
                return 1;
            }
        }
        else
        {
            Log.Info("Creating new workshop item...");

            SteamAPICall_t createItemCall = SteamUGC.CreateItem(_sts2AppId, EWorkshopFileType.k_EWorkshopFileTypeCommunity);
            using SteamCallResult<CreateItemResult_t> createItemCallResult = new(createItemCall);
            CreateItemResult_t createItemResult = await createItemCallResult.Task;

            if (createItemResult.m_eResult != EResult.k_EResultOK)
            {
                Log.Error($"Failed to create workshop item! Result: {createItemResult.m_eResult}");
                return 1;
            }

            workshopItem = createItemResult.m_nPublishedFileId;
        }
        
        Log.Info($"Uploading '{modConfig.title}' to the steam workshop with item ID {workshopItem.m_PublishedFileId}...");

        UGCUpdateHandle_t updateHandle = SteamUGC.StartItemUpdate(_sts2AppId, workshopItem);

        if (modConfig.title != null)
        {
            if (!SteamUGC.SetItemTitle(updateHandle, modConfig.title))
            {
                Log.Warn("Failed to set title!");
            }
        }

        if (modConfig.description != null)
        {
            if (!SteamUGC.SetItemDescription(updateHandle, modConfig.description))
            {
                Log.Warn("Failed to set description!");
            }
        }

        if (modConfig.visibility != null)
        {
            ERemoteStoragePublishedFileVisibility visibility = VisibiltyFromString(modConfig.visibility)!.Value;
            
            if (!SteamUGC.SetItemVisibility(updateHandle, visibility))
            {
                Log.Warn("Failed to set visibility!");
            }
        }

        if (modConfig.tags != null)
        {
            if (!SteamUGC.SetItemTags(updateHandle, modConfig.tags))
            {
                Log.Warn("Failed to set tags!");
            }
        }

        if (!SteamUGC.SetRequiredGameVersions(updateHandle, modConfig.minBranch ?? "", modConfig.maxBranch ?? ""))
        {
            Log.Warn("Failed to set required game versions!");
        }

        if (!SteamUGC.SetItemContent(updateHandle, contentDirectoryInfo.FullName))
        {
            Log.Warn("Failed to upload content!");
        }

        if (!SteamUGC.SetItemPreview(updateHandle, imageFileInfo.FullName))
        {
            Log.Warn("Failed to set preview image!");
        }

        SteamAPICall_t updateItemCall = SteamUGC.SubmitItemUpdate(updateHandle, modConfig.changeNote ?? "");
        using SteamCallResult<SubmitItemUpdateResult_t> updateItemCallResult = new(updateItemCall);

        while (!updateItemCallResult.Task.IsCompleted)
        {
            await Task.Delay(500);
            
            EItemUpdateStatus status =
                SteamUGC.GetItemUpdateProgress(updateHandle, out ulong bytesProcessed, out ulong bytesTotal);

            if (bytesTotal > 0)
            {
                Log.Info($"Status: {status}, bytes processed: {bytesProcessed}/{bytesTotal} ({(float)bytesProcessed/bytesTotal:P2})");
            }
            else
            {
                Log.Info($"Status: {status}");
            }
        }
        
        SubmitItemUpdateResult_t updateItemResult = await updateItemCallResult.Task;

        if (updateItemResult.m_eResult != EResult.k_EResultOK)
        {
            Log.Error($"Error occurred while uploading to the workshop! Result: {updateItemResult.m_eResult}");
            return 1;
        }

        bool localizedOk = await UpdateLocalizedMetadata(
            workshopItem,
            modConfig.localizedTitles,
            localizedDescriptions);
        if (!localizedOk)
        {
            return 1;
        }
        
        await UpdateDependencies(workshopItem, modConfig.dependencies ?? []);

        Log.Info($"Successfully uploaded '{modConfig.title}' to the workshop with id {workshopItem.m_PublishedFileId}! Browsing to the item in Steam.");
        SteamFriends.ActivateGameOverlayToWebPage($"steam://url/CommunityFilePage/{workshopItem.m_PublishedFileId}");
        
        // Since we successfully uploaded, if it didn't exist already, put a mod_id.txt in the directory for later, to
        // identify which mod ID this is.
        if (modIdTxt == null || modIdTxt.Value != workshopItem.m_PublishedFileId)
        {
            await using FileStream fileStream = modIdFile.Open(FileMode.Create);
            await using StreamWriter writer = new(fileStream);
            writer.WriteLine(workshopItem.m_PublishedFileId);
        }
        
        return 0;
    }

    private static async Task UpdateDependencies(PublishedFileId_t workshopItem, List<ulong> newDependencies)
    {
        List<ulong> existingDependencies = await GetAppDependencies(workshopItem);
        bool modified = false;
        
        // Iterate new dependencies, adding dependencies that didn't exist
        foreach (ulong dependency in newDependencies)
        {
            if (!existingDependencies.Contains(dependency))
            {
                SteamUGC.AddDependency(workshopItem, new PublishedFileId_t(dependency));
                Log.Info($"Added dependency on {dependency}");
                modified = true;
            }
        }
        
        // Iterate existing dependencies, removing dependencies that no longer exist
        foreach (ulong dependency in existingDependencies)
        {
            if (!newDependencies.Contains(dependency))
            {
                SteamUGC.RemoveDependency(workshopItem, new PublishedFileId_t(dependency));
                Log.Info($"Removed dependency on {dependency}");
                modified = true;
            }
        }

        if (!modified)
        {
            Log.Info("No modifications were made to dependencies.");
        }
    }

    private static async Task<List<ulong>> GetAppDependencies(PublishedFileId_t workshopItem)
    {
        Log.Info("Querying existing app dependencies... ");
        
        UGCQueryHandle_t handle = SteamUGC.CreateQueryUGCDetailsRequest([workshopItem], 1);

        try
        {
            // Children (dependencies) are only populated in the query results if we explicitly request them.
            SteamUGC.SetReturnChildren(handle, true);

            SteamAPICall_t call = SteamUGC.SendQueryUGCRequest(handle);
            using SteamCallResult<SteamUGCQueryCompleted_t> callResult = new(call);
            SteamUGCQueryCompleted_t result = await callResult.Task;

            if (result.m_eResult != EResult.k_EResultOK)
            {
                Log.Warn(
                    $"Couldn't get dependencies for item {workshopItem.m_PublishedFileId}! Error: {result.m_eResult}");
                return [];
            }

            if (!SteamUGC.GetQueryUGCResult(handle, 0, out SteamUGCDetails_t details))
            {
                Log.Warn($"Couldn't read query result for item {workshopItem.m_PublishedFileId}.");
                return [];
            }

            uint numChildren = details.m_unNumChildren;
            if (numChildren == 0)
            {
                return [];
            }

            // GetQueryUGCChildren returns all children of the item (at result index 0) in a single call.
            // The array must be sized to the number of children; there is no pagination for children.
            PublishedFileId_t[] cache = new PublishedFileId_t[numChildren];
            if (!SteamUGC.GetQueryUGCChildren(handle, 0, cache, numChildren))
            {
                Log.Warn($"Failed to read dependencies for item {workshopItem.m_PublishedFileId}.");
                return [];
            }

            List<ulong> dependencies = [];
            foreach (PublishedFileId_t dependency in cache)
            {
                if (dependency.m_PublishedFileId != 0)
                {
                    dependencies.Add(dependency.m_PublishedFileId);
                }
            }

            if (dependencies.Count > 0)
            {
                Log.Info($"Found {dependencies.Count} dependencies.");
            }

            return dependencies;
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    private static async Task<bool> DoesWorkshopItemExist(PublishedFileId_t workshopItem)
    {
        UGCQueryHandle_t handle = SteamUGC.CreateQueryUGCDetailsRequest([workshopItem], 1);

        try
        {
            SteamAPICall_t call = SteamUGC.SendQueryUGCRequest(handle);
            using SteamCallResult<SteamUGCQueryCompleted_t> callResult = new(call);
            SteamUGCQueryCompleted_t result = await callResult.Task;

            SteamUGC.GetQueryUGCResult(handle, 0, out SteamUGCDetails_t details);

            if (details.m_eResult == EResult.k_EResultFileNotFound)
            {
                return false;
            }
            else if (details.m_eResult != EResult.k_EResultOK)
            {
                Log.Warn($"Couldn't confirm existence of workshop item {workshopItem.m_PublishedFileId}. Error: {result.m_eResult}");
            }

            return true;
        }
        finally
        {
            SteamUGC.ReleaseQueryUGCRequest(handle);
        }
    }

    private static ERemoteStoragePublishedFileVisibility? VisibiltyFromString(string visibility)
    {
        return visibility switch
        {
            "private" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPrivate,
            "public" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityPublic,
            "unlisted" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityUnlisted,
            "friends_only" => ERemoteStoragePublishedFileVisibility.k_ERemoteStoragePublishedFileVisibilityFriendsOnly,
            _ => null
        };
    }

    private static async Task<bool> UpdateLocalizedMetadata(
        PublishedFileId_t workshopItem,
        Dictionary<string, string>? localizedTitles,
        Dictionary<string, string>? localizedDescriptions)
    {
        HashSet<string> languages = [];
        if (localizedTitles != null)
        {
            foreach (string language in localizedTitles.Keys)
            {
                if (!string.IsNullOrWhiteSpace(language))
                {
                    languages.Add(language.Trim());
                }
            }
        }

        if (localizedDescriptions != null)
        {
            foreach (string language in localizedDescriptions.Keys)
            {
                if (!string.IsNullOrWhiteSpace(language))
                {
                    languages.Add(language.Trim());
                }
            }
        }

        if (languages.Count == 0)
        {
            return true;
        }

        foreach (string language in languages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            UGCUpdateHandle_t handle = SteamUGC.StartItemUpdate(_sts2AppId, workshopItem);

            if (!SteamUGC.SetItemUpdateLanguage(handle, language))
            {
                Log.Error($"Failed to switch workshop update language to '{language}'.");
                return false;
            }

            bool hasAnyField = false;
            string? title = null;
            string? description = null;
            bool hasTitle = localizedTitles != null && localizedTitles.TryGetValue(language, out title);
            bool hasDescription = localizedDescriptions != null &&
                                  localizedDescriptions.TryGetValue(language, out description);

            if (hasTitle &&
                !string.IsNullOrWhiteSpace(title) &&
                !SteamUGC.SetItemTitle(handle, title))
            {
                Log.Warn($"Failed to set localized title for '{language}'.");
            }
            else if (!string.IsNullOrWhiteSpace(title))
            {
                hasAnyField = true;
            }

            if (hasDescription &&
                !string.IsNullOrWhiteSpace(description) &&
                !SteamUGC.SetItemDescription(handle, description))
            {
                Log.Warn($"Failed to set localized description for '{language}'.");
            }
            else if (!string.IsNullOrWhiteSpace(description))
            {
                hasAnyField = true;
            }

            if (!hasAnyField)
            {
                continue;
            }

            SteamAPICall_t updateCall = SteamUGC.SubmitItemUpdate(handle, "");
            using SteamCallResult<SubmitItemUpdateResult_t> updateResultCall = new(updateCall);
            SubmitItemUpdateResult_t updateResult = await updateResultCall.Task;
            if (updateResult.m_eResult != EResult.k_EResultOK)
            {
                Log.Error($"Failed to submit localized metadata for '{language}'. Result: {updateResult.m_eResult}");
                return false;
            }

            Log.Info($"Updated localized title/description for '{language}'.");
        }

        return true;
    }

    private static Dictionary<string, string>? BuildLocalizedDescriptions(
        DirectoryInfo workspaceDirectory,
        DirectoryInfo contentDirectoryInfo,
        Dictionary<string, string>? existingLocalizedDescriptions)
    {
        Dictionary<string, string> merged = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> kv in existingLocalizedDescriptions ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            {
                merged[kv.Key.Trim()] = kv.Value;
            }
        }

        foreach (KeyValuePair<string, string> fromFile in LoadLocalizedDescriptionsFromFiles(workspaceDirectory, contentDirectoryInfo))
        {
            merged[fromFile.Key] = fromFile.Value;
        }

        return merged.Count == 0 ? null : merged;
    }

    private static Dictionary<string, string> LoadLocalizedDescriptionsFromFiles(
        DirectoryInfo workspaceDirectory,
        DirectoryInfo contentDirectoryInfo)
    {
        Dictionary<string, string> localized = new(StringComparer.OrdinalIgnoreCase);
        List<string> candidateDirectories =
        [
            Path.Combine(contentDirectoryInfo.FullName, "description"),
            Path.Combine(contentDirectoryInfo.FullName, "descriptions"),
            Path.Combine(workspaceDirectory.FullName, "description"),
            Path.Combine(workspaceDirectory.FullName, "descriptions")
        ];

        HashSet<string> seenDirs = new(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in candidateDirectories)
        {
            if (!seenDirs.Add(dir) || !Directory.Exists(dir))
            {
                continue;
            }

            foreach (string markdownPath in Directory.EnumerateFiles(dir, "*.md", SearchOption.TopDirectoryOnly))
            {
                string stem = Path.GetFileNameWithoutExtension(markdownPath);
                string? language = NormalizeLanguageCode(stem);
                if (language == null)
                {
                    Log.Warn($"Skipped localized description file '{markdownPath}' because language code could not be recognized.");
                    continue;
                }

                string markdown = File.ReadAllText(markdownPath);
                if (string.IsNullOrWhiteSpace(markdown))
                {
                    continue;
                }

                localized[language] = MarkdownToBbCode(markdown);
                Log.Info($"Loaded localized description for '{language}' from '{markdownPath}'.");
            }
        }

        return localized;
    }

    private static string? NormalizeLanguageCode(string raw)
    {
        string key = raw.Trim().ToLowerInvariant().Replace("_", "-");
        Dictionary<string, string> aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "english",
            ["eng"] = "english",
            ["english"] = "english",
            ["zh"] = "schinese",
            ["zhs"] = "schinese",
            ["zh-cn"] = "schinese",
            ["cn"] = "schinese",
            ["schinese"] = "schinese",
            ["zht"] = "tchinese",
            ["zh-tw"] = "tchinese",
            ["tchinese"] = "tchinese",
            ["tw"] = "tchinese",
            ["ja"] = "japanese",
            ["jp"] = "japanese",
            ["jpn"] = "japanese",
            ["japanese"] = "japanese",
            ["ko"] = "koreana",
            ["kr"] = "koreana",
            ["kor"] = "koreana",
            ["korean"] = "koreana",
            ["koreana"] = "koreana",
            ["fr"] = "french",
            ["fra"] = "french",
            ["fre"] = "french",
            ["french"] = "french",
            ["de"] = "german",
            ["ger"] = "german",
            ["deu"] = "german",
            ["german"] = "german",
            ["es"] = "spanish",
            ["spa"] = "spanish",
            ["spanish"] = "spanish",
            ["ru"] = "russian",
            ["rus"] = "russian",
            ["russian"] = "russian",
            ["it"] = "italian",
            ["ita"] = "italian",
            ["italian"] = "italian",
            ["pl"] = "polish",
            ["pol"] = "polish",
            ["polish"] = "polish",
            ["pt"] = "portuguese",
            ["pt-pt"] = "portuguese",
            ["portuguese"] = "portuguese",
            ["br"] = "brazilian",
            ["pt-br"] = "brazilian",
            ["brazilian"] = "brazilian",
            ["th"] = "thai",
            ["tha"] = "thai",
            ["thai"] = "thai"
        };

        return aliases.TryGetValue(key, out string? code) ? code : null;
    }

    private static string MarkdownToBbCode(string markdown)
    {
        string text = markdown.Replace("\r\n", "\n");

        text = Regex.Replace(text, "```([\\s\\S]*?)```", m =>
        {
            string code = m.Groups[1].Value.Trim('\n');
            return $"[code]{code}[/code]";
        }, RegexOptions.Multiline);

        text = Regex.Replace(text, @"(?m)^#{1,6}\s*(.+)$", "[b]$1[/b]");
        text = Regex.Replace(text, @"\[(.+?)\]\((https?://[^\s)]+)\)", "[url=$2]$1[/url]");
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", "[b]$1[/b]");
        text = Regex.Replace(text, @"__(.+?)__", "[b]$1[/b]");
        text = Regex.Replace(text, @"(?<!\*)\*(?!\s)(.+?)(?<!\s)\*(?!\*)", "[i]$1[/i]");
        text = Regex.Replace(text, @"(?<!_)_(?!\s)(.+?)(?<!\s)_(?!_)", "[i]$1[/i]");
        text = Regex.Replace(text, @"`([^`]+)`", "[code]$1[/code]");

        string[] lines = text.Split('\n');
        List<string> output = [];
        bool inList = false;

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimEnd();
            Match ul = Regex.Match(line, @"^\s*[-*+]\s+(.+)$");
            Match ol = Regex.Match(line, @"^\s*\d+\.\s+(.+)$");
            bool isList = ul.Success || ol.Success;

            if (isList)
            {
                if (!inList)
                {
                    output.Add("[list]");
                    inList = true;
                }

                string item = ul.Success ? ul.Groups[1].Value : ol.Groups[1].Value;
                output.Add($"[*]{item}");
                continue;
            }

            if (inList)
            {
                output.Add("[/list]");
                inList = false;
            }

            output.Add(line);
        }

        if (inList)
        {
            output.Add("[/list]");
        }

        return string.Join(Environment.NewLine, output).Trim();
    }
}