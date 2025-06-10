using System.Text.Json;
using Steamworks;

namespace ModUploader;

public static class UploadCommand
{
    private static AppId_t _sts2AppId = new(2868840);
    private static bool _steamIsInitialized;
    
    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };
    
    public static async Task<int> UploadWorkspace(DirectoryInfo workspaceDirectory, ulong? itemIdArg)
    {
        // First, do some validation of what is in the directory.
        FileInfo imageFileInfo = new FileInfo(Path.Combine(workspaceDirectory.FullName, "image.jpg"));
        if (!imageFileInfo.Exists)
        {
            Log.Info("There is no file named image.jpg in the workspace!");
            return 1;
        }

        DirectoryInfo contentDirectoryInfo = new DirectoryInfo(Path.Combine(workspaceDirectory.FullName, "content"));
        if (!contentDirectoryInfo.Exists)
        {
            Log.Info("There is no 'content' directory inside the workspace!");
            return 1;
        }

        FileInfo configJsonInfo = new FileInfo(Path.Combine(workspaceDirectory.FullName, "config.json"));
        if (!configJsonInfo.Exists)
        {
            Log.Info($"There is no file named config.json in the workspace!");
            return 1;
        }

        ModConfig? modConfig;
        
        try
        {
            await using FileStream configJsonStream = configJsonInfo.Open(FileMode.Open);
            modConfig = await JsonSerializer.DeserializeAsync<ModConfig>(configJsonStream, _serializerOptions);
        }
        catch (JsonException)
        {
            Log.Info($"Exception thrown while parsing your mod config! Double-check that the format is correct.");
            throw;
        }

        if (modConfig == null)
        {
            Log.Info($"Tried to parse config.json, but it returned null!");
            return 1;
        }

        if (VisibiltyFromString(modConfig.visibility) == null)
        {
            Log.Info($"Invalid visibility '{modConfig.visibility}' in config.json! Should be: private, public, unlisted, or friends_only");
            return 1;
        }

        ulong? modIdTxt = null;
        
        FileInfo modIdFile = new(Path.Combine(workspaceDirectory.FullName, "mod_id.txt"));
        if (modIdFile.Exists)
        {
            await using FileStream modIdStream = modIdFile.OpenRead();
            using StreamReader reader = new(modIdStream);
            string modIdStr = (await reader.ReadToEndAsync()).Trim();

            if (!ulong.TryParse(modIdStr, out ulong modId))
            {
                Log.Info("Tried to read mod ID from mod_id.txt, but the text could not be parsed as a mod ID!");
                return 1;
            }

            modIdTxt = modId;
        }

        // Validation is all done. Start the upload process.
        Log.Info("Initializing Steam");

        try
        {
            ESteamAPIInitResult result = SteamAPI.InitEx(out string initErrorMessage);

            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
            {
                Log.Info($"Steam initialization failed! Result: {result}, message: {initErrorMessage}");
                return 1;
            }
        }
        catch (Exception e)
        {
            Log.Info($"Steam initialization threw an exception: {e}");
            return 1;
        }
        
        // Start running callbacks, otherwise we will never get steam call results
        _steamIsInitialized = true;
        _ = DoRunCallbacks();
        
        Log.Info("--------");
        Log.Info($"By submitting '{modConfig.title}' to the workshop, you agree to the Steam Workshop terms of service:\nhttps://steamcommunity.com/sharedfiles/workshoplegalagreement");
        Log.Info("--------");

        PublishedFileId_t workshopItem;

        if (itemIdArg != null)
        {
            Log.Info($"Uploading to ID {itemIdArg.Value} passed in command line");
            workshopItem = new PublishedFileId_t(itemIdArg.Value);
        }
        else if (modIdTxt != null)
        {
            Log.Info($"Uploading to ID {modIdTxt.Value} from mod_id.txt");
            workshopItem = new PublishedFileId_t(modIdTxt.Value);
        }
        else
        {
            Log.Info("Creating new workshop item...");

            SteamAPICall_t createItemCall = SteamUGC.CreateItem(_sts2AppId, EWorkshopFileType.k_EWorkshopFileTypeCommunity);
            using SteamCallResult<CreateItemResult_t> createItemCallResult = new(createItemCall);
            CreateItemResult_t createItemResult = await createItemCallResult.Task;

            if (createItemResult.m_eResult != EResult.k_EResultOK)
            {
                Log.Info($"Failed to create workshop item! Result: {createItemResult.m_eResult}");
                return 1;
            }

            workshopItem = createItemResult.m_nPublishedFileId;
        }
        
        Log.Info($"Uploading '{modConfig.title}' to the steam workshop with item ID {workshopItem.m_PublishedFileId}...");

        UGCUpdateHandle_t updateHandle = SteamUGC.StartItemUpdate(_sts2AppId, workshopItem);

        SteamUGC.SetItemTitle(updateHandle, modConfig.title);
        SteamUGC.SetItemDescription(updateHandle, modConfig.description);
        SteamUGC.SetItemVisibility(updateHandle, VisibiltyFromString(modConfig.visibility)!.Value);
        SteamUGC.SetItemTags(updateHandle, modConfig.tags);
        SteamUGC.SetItemContent(updateHandle, contentDirectoryInfo.FullName);
        SteamUGC.SetItemPreview(updateHandle, imageFileInfo.FullName);

        SteamAPICall_t updateItemCall = SteamUGC.SubmitItemUpdate(updateHandle, modConfig.changeNote);
        using SteamCallResult<SubmitItemUpdateResult_t> updateItemCallResult = new(updateItemCall);

        CancellationTokenSource uploadProgressCancelToken = new();
        _ = LogUploadProgress(updateHandle, uploadProgressCancelToken);
        
        SubmitItemUpdateResult_t updateItemResult = await updateItemCallResult.Task;

        if (updateItemResult.m_eResult != EResult.k_EResultOK)
        {
            Log.Info($"Error occurred while uploading to the workshop! Result: {updateItemResult.m_eResult}");
            return 1;
        }

        Log.Info($"Successfully uploaded '{modConfig.title}' to the workshop with id {workshopItem.m_PublishedFileId}! Browsing to the item in Steam.");
        SteamFriends.ActivateGameOverlayToWebPage($"steam://url/CommunityFilePage/{workshopItem.m_PublishedFileId}");
        
        // Since we successfully uploaded, if it didn't exist already, put a mod_id.txt in the directory for later, to
        // identify which mod ID this is.
        if (modIdTxt == null || modIdTxt.Value != workshopItem.m_PublishedFileId)
        {
            await using FileStream fileStream = modIdFile.OpenWrite();
            await using StreamWriter writer = new(fileStream);
            writer.WriteLine(workshopItem.m_PublishedFileId);
        }

        SteamAPI.Shutdown();
        _steamIsInitialized = false;
        
        return 0;
    }

    private static async Task LogUploadProgress(UGCUpdateHandle_t updateHandle, CancellationTokenSource cancelToken)
    {
        while (!cancelToken.IsCancellationRequested)
        {
            EItemUpdateStatus status =
                SteamUGC.GetItemUpdateProgress(updateHandle, out ulong bytesProcessed, out ulong bytesTotal);
            Log.Info($"Status: {status}, bytes processed: {bytesProcessed}/{bytesTotal} ({bytesProcessed/bytesTotal:P2})");
            await Task.Delay(1000, cancelToken.Token);
        }
    }

    private static async Task DoRunCallbacks()
    {
        // RunCallbacks must be run periodically to flush call results
        while (_steamIsInitialized)
        {
            SteamAPI.RunCallbacks();
            await Task.Delay(50);
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
}