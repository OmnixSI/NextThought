namespace NextThought.Desktop;

public partial class AppState
{

    [RelayCommand]
    private void SetScene(MeetingMode mode)
    {
        SelectedMode = mode;
        OnPropertyChanged(nameof(SelectedScene));
        OnSceneChanged();
    }

    [RelayCommand]
    private void Navigate(string section)
    {
        if (Enum.TryParse<AppSection>(section, out var value)) Section = value;
    }

    [RelayCommand]
    private void ReloadProcesses()
    {
        Settings.RefreshProcesses();
        SetStatus("已刷新可采集的应用程序列表。");
    }

    [RelayCommand]
    private void DismissCoach() => CoachHint = "";

    [RelayCommand]
    private void PreviousSlide() => MoveSlide(-1);

    [RelayCommand]
    private void NextSlide() => MoveSlide(1);

    [RelayCommand(CanExecute = nameof(CanSummarize))]
    private async Task SummarizeAsync()
    {
        if (CurrentSession is { } meta) await SummarizeCoreAsync(meta);
    }

    [RelayCommand(CanExecute = nameof(CanImportWave))]
    private async Task ImportWaveAsync()
    {
        if (PickOpenFile is null) return;
        if (await PickOpenFile("WAV 录音 (*.wav)|*.wav") is not { } file) return;
        await ImportWaveFileAsync(file);
    }

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private async Task SaveSettingsAsync()
    {
        try
        {
            await Settings.SaveAsync();
            if (!SettingsStore.CloudSttKeyReadFailed) SettingsStore.SaveCloudSttKey(Settings.CloudSttKey);
            LanguageChanged?.Invoke(UiText.ResolveEnglish(Settings.Language));
            ApplyAppearance();
            RefreshModelState();
            SetStatus("设置已保存，密钥已用当前 Windows 用户加密。");
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    [RelayCommand]
    private void Cancel() { workCancellation?.Cancel(); requestCancellation?.Cancel(); translator?.Cancel(); }

    [RelayCommand]
    private void OpenData() => OpenExternal(Paths.Root);

    [RelayCommand]
    private async Task PrepareModelsAsync()
    {
        await RunWorkAsync(async ct =>
        {
            var settings = Settings.Build();
            await Settings.SaveAsync();
            var progress = new Progress<ModelProgress>(p => { Progress = p.Fraction; SetStatus(p.Message); });
            await Models.InstallAsync(settings.LocalModel, progress, ct);
            if (settings.SecondPass) await Models.InstallAsync("xasrOffline", progress, ct);
            SetStatus("模型已下载并校验，可以离线记录。");
        });
    }

    [RelayCommand]
    private async Task PrepareSpeakersAsync() => await RunWorkAsync(async ct =>
    {
        var progress = new Progress<ModelProgress>(p => { Progress = p.Fraction; SetStatus(p.Message); });
        await new SpeakerModelStore(Paths).InstallAsync(progress, ct);
        SetStatus("声纹模型已准备，可在设置中开启并保存。");
    });

    [RelayCommand]
    private async Task PrepareRetrievalAsync() => await RunWorkAsync(async ct =>
    {
        var progress = new Progress<ModelProgress>(p => { Progress = p.Fraction; SetStatus(p.Message); });
        await new EmbeddingModelStore(Paths).InstallAsync(progress, ct);
        SetStatus("语义检索模型已准备，勾选语义检索并保存设置后使用。");
    });

    private void LatchCompanionCommandState() => OnPropertyChanged(nameof(CompanionRunning));

    // ---- 手机伴侣（方案第 12 节，局域网通道；平台层 CompanionServer 已验收） ----

    private CompanionServer? companion;

    private CompanionRelayClient? relay;

    [RelayCommand]
    private void RefreshCompanionAddresses()
    {
        CompanionAddresses.Clear();
        foreach (var address in CompanionServer.Addresses()) CompanionAddresses.Add(address);
        SelectedCompanionAddress = CompanionAddresses.FirstOrDefault();
        CompanionInfo = CompanionAddresses.Count == 0
            ? "没有找到可用的局域网 IPv4 地址；请先连接 Wi-Fi 或以太网。"
            : "默认关闭；开启后手机与电脑需在同一可信网络。首次监听时 Windows 防火墙会询问，请只允许专用网络。";
    }

    [RelayCommand(CanExecute = nameof(CanToggleCompanion))]
    private async Task ToggleCompanionAsync()
    {
        if (companion is not null || relay is not null)
        {
            if (companion is not null) { await companion.DisposeAsync(); companion = null; }
            if (relay is not null) { await relay.DisposeAsync(); relay = null; }
            CompanionRunning = false;
            CompanionQr = null;
            CompanionLink = "";
            SetStatus("手机伴侣已关闭。");
            return;
        }
        var relayUrl = Settings.Build().CompanionRelayUrl.Trim();
        var address = SelectedCompanionAddress;
        if (address is null && relayUrl.Length == 0)
        {
            SetStatus("请先选择一个局域网地址，或在设置里填写中转地址。", true);
            return;
        }
        try
        {
            if (address is not null)
            {
                companion = new CompanionServer(address.Address, HandleCompanionActionAsync);
                CompanionLink = companion.PairingUrl;
                CompanionInfo = UiText.Format("已在 %@:%lld 监听 · 配对链接相当于临时遥控密钥，只在可信网络使用。", address.Address, companion.BaseUri.Port);
            }
            if (relayUrl.Length > 0) await StartRelayAsync(relayUrl);
            CompanionRunning = true;
            CompanionQr = BuildQr(CompanionLink);
            PublishCompanionSnapshot();
            SetStatus(relayUrl.Length > 0 ? "手机伴侣已开启（中转通道）；手机扫码即可查看当前问题与回答。" : "手机伴侣已开启；手机扫码即可查看当前问题与回答。");
        }
        catch (Exception ex)
        {
            if (companion is not null) { await companion.DisposeAsync(); companion = null; }
            if (relay is not null) { await relay.DisposeAsync(); relay = null; }
            CompanionRunning = false;
            SetStatus("手机伴侣启动失败：" + ex.Message, true);
        }
    }

    private bool CanToggleCompanion() => !ShuttingDown;

    public Task<bool> HandleCompanionActionForCheckAsync(string action) => HandleCompanionActionAsync(action);

    private async Task<bool> HandleCompanionActionAsync(string action)
    {
        // Network threads must not touch UI-bound state; marshal and await on the UI thread.
        if (Dispatcher.UIThread.CheckAccess()) return await HandleCompanionActionCoreAsync(action);
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(async () =>
        {
            try { completion.SetResult(await HandleCompanionActionCoreAsync(action)); }
            catch (Exception exception) { completion.SetException(exception); }
        });
        return await completion.Task;
    }

    private void PublishCompanionSnapshot()
    {
        var snapshot = CurrentCompanionSnapshot();
        companion?.Publish(snapshot);
        relay?.Publish(snapshot);
    }
}
