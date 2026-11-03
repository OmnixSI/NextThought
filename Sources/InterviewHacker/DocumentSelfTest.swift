import AppKit
import Foundation
import PDFKit
import InterviewHackerCore

enum DocumentSelfTest {
    static func run(_ options:[String:String]) async throws -> String {
        guard let input=options["input"],let out=options["output"] else { throw SelfTest.Failure("documents requires --input file-or-folder --output directory; optional --provider system|paddle|codex") }
        let provider=DocumentOCRProvider(rawValue:options["provider"] ?? "system") ?? .system
        let url=URL(fileURLWithPath:input), target=URL(fileURLWithPath:out,isDirectory:true)
        try FileManager.default.createDirectory(at:target,withIntermediateDirectories:true)
        let urls=DocumentImporter.expand(url).filter { $0.lastPathComponent != "reference.json" }
        guard !urls.isEmpty else { throw SelfTest.Failure("no fixtures") }
        var records:[[String:Any]]=[];var failures=0
        for source in urls {
            let start=Date()
            do {
                let converted=try await MarkdownConverter.convert(source,options:.init(provider:provider,fallback:false,model:options["model"] ?? "gpt-6-luna",effort:"medium",timeout:600,localModelID:options["ocr-model"] ?? ModelCatalog.paddleOCR.id))
                try converted.markdown.write(to:target.appendingPathComponent(source.lastPathComponent+".md"),atomically:true,encoding:.utf8)
                if let content=converted.content { let encoder=JSONEncoder();encoder.outputFormatting=[.prettyPrinted,.sortedKeys];try encoder.encode(content).write(to:target.appendingPathComponent(source.lastPathComponent+".json")) }
                records.append(["file":source.lastPathComponent,"seconds":Date().timeIntervalSince(start),"characters":converted.characterCount,"status":converted.content?.status ?? "unknown"])
                print("DOCUMENT ✔ \(provider.rawValue) \(source.lastPathComponent) \(Date().timeIntervalSince(start))s")
            } catch {
                failures+=1;records.append(["file":source.lastPathComponent,"seconds":Date().timeIntervalSince(start),"error":error.localizedDescription])
                print("DOCUMENT ✘ \(provider.rawValue) \(source.lastPathComponent): \(error)")
            }
        }
        try JSONSerialization.data(withJSONObject:["provider":provider.rawValue,"os":ProcessInfo.processInfo.operatingSystemVersionString,"records":records],options:[.prettyPrinted,.sortedKeys]).write(to:target.appendingPathComponent("summary.json"))
        if failures>0 { throw SelfTest.Failure("\(failures)/\(urls.count) documents failed") }
        return "\(urls.count) files, provider=\(provider.rawValue), raw artifacts=\(out)"
    }
    static func runtime(_ options: [String:String]) async throws -> String {
        guard let path=options["archive"] else { throw SelfTest.Failure("runtime requires --archive; destination is INTERVIEWHACKER_RUNTIME_DIR or isolated data directory") }
        let manifest=try JSONDecoder().decode(DocumentRuntimeInstaller.Manifest.self,from:Data(contentsOf:DocumentRuntimeInstaller.manifestURL))
        try await DocumentRuntimeInstaller.installArchive(URL(fileURLWithPath:path),manifest:manifest)
        let result=try await DocumentWorker.shared.request("health")
        guard result["protocol"] as? Int == 1 else { throw SelfTest.Failure("runtime health protocol") }
        await DocumentWorker.shared.unload()
        return "SHA-256, signature, extraction, relocation and NDJSON health passed"
    }
    static func model() async throws -> String {
        let embedder=await SemanticRetrievalService.shared.embedder()
        let q=try await embedder.embedQuery("Kubernetes 调度器", timeout: 120)
        let good=try await embedder.embedPassage("Kubernetes 的调度器把 Pod 分配到合适的节点。")
        let bad=try await embedder.embedPassage("今天中午吃意大利面。")
        func dot(_ a:[Float],_ b:[Float])->Float { zip(a,b).reduce(0){$0+$1.0*$1.1} }
        guard q.count==1024,dot(q,good)>dot(q,bad) else { throw SelfTest.Failure("embedding semantic check failed") }
        return "1024 dimensions, related=\(dot(q,good)) unrelated=\(dot(q,bad))"
    }
}

extension DocumentSelfTest {
    static func edges(_ options:[String:String]) async throws -> String {
        guard let fixtures=options["fixtures"] else { throw SelfTest.Failure("document-edges requires --fixtures directory") }
        let directory=DocumentRuntime.temporary.appendingPathComponent("edges-"+UUID().uuidString,isDirectory:true)
        try FileManager.default.createDirectory(at:directory,withIntermediateDirectories:true)
        defer { try? FileManager.default.removeItem(at:directory) }
        let empty=directory.appendingPathComponent("empty.txt");try Data().write(to:empty)
        let large=directory.appendingPathComponent("large.txt")
        FileManager.default.createFile(atPath: large.path, contents: nil)
        let handle = try FileHandle(forWritingTo: large); try handle.truncate(atOffset: UInt64(DocumentLimits.maximumFileBytes) + 1); try handle.close()
        let invalid=try await DocumentImporter.read([empty,large],categories:[])
        guard invalid.documents.isEmpty,invalid.failures.count==2 else { throw SelfTest.Failure("empty/oversize rejection") }
        let base=URL(fileURLWithPath:fixtures,isDirectory:true)
        guard let first=PDFDocument(url:base.appendingPathComponent("01-chinese-cv.pdf")),let second=PDFDocument(url:base.appendingPathComponent("02-english-jd.pdf")) else { throw SelfTest.Failure("PDF fixture missing") }
        let mixed=PDFDocument();mixed.insert(first.page(at:0)!,at:0);mixed.insert(second.page(at:0)!,at:1)
        let mixedFile=directory.appendingPathComponent("mixed.pdf");mixed.write(to:mixedFile)
        let converted=try await MarkdownConverter.convert(mixedFile,options:.init())
        guard Set(converted.content!.blocks.compactMap { $0.locator.page })==[1,2],converted.markdown.contains("王明"),converted.markdown.contains("Backend Engineer") else { throw SelfTest.Failure("mixed PDF loses physical pages") }
        let encrypted=directory.appendingPathComponent("encrypted.pdf")
        mixed.write(to:encrypted,withOptions:[.userPasswordOption:"secret",.ownerPasswordOption:"owner"])
        do { _ = try await MarkdownConverter.convert(encrypted,options:.init());throw SelfTest.Failure("encrypted PDF accepted") }
        catch is SelfTest.Failure { throw SelfTest.Failure("encrypted PDF accepted") } catch { }
        let original=getenv("INTERVIEWHACKER_DOCUMENT_MODELS_DIR").map { String(cString:$0) }
        setenv("INTERVIEWHACKER_DOCUMENT_MODELS_DIR",directory.appendingPathComponent("missing-models").path,1)
        defer { if let original { setenv("INTERVIEWHACKER_DOCUMENT_MODELS_DIR",original,1) } else { unsetenv("INTERVIEWHACKER_DOCUMENT_MODELS_DIR") } }
        let image=base.appendingPathComponent("03-bilingual-columns.png")
        let fallback=try await MarkdownConverter.convert(image,options:.init(provider:.paddle,fallback:true))
        guard fallback.content?.status=="degraded",fallback.content?.warnings.isEmpty==false else { throw SelfTest.Failure("fallback not marked") }
        do { _ = try await MarkdownConverter.convert(image,options:.init(provider:.paddle,fallback:false));throw SelfTest.Failure("strict conversion silently falls back") }
        catch is SelfTest.Failure { throw SelfTest.Failure("strict conversion silently falls back") } catch { }
        // Cancellation must release and terminate the actual worker, then a new request succeeds.
        let task=Task { try await DocumentWorker.shared.request("convert",["path":base.appendingPathComponent("06-office-embedded.docx").path,"output_directory":directory.path]) }
        task.cancel()
        do { _ = try await task.value;throw SelfTest.Failure("worker cancellation ignored") } catch is CancellationError { }
        let health=try await DocumentWorker.shared.request("health")
        guard health["protocol"] as? Int==1 else { throw SelfTest.Failure("worker recovery") }
        await DocumentWorker.shared.unload()
        return "empty, oversized, encrypted, mixed PDF, physical locators, explicit fallback, strict mode, cancellation and worker recovery passed"
    }
}

extension DocumentSelfTest {
    @MainActor static func lifecycle() async throws -> String {
        guard DemoMode.isEnabled else { throw SelfTest.Failure("document-lifecycle requires INTERVIEWHACKER_DEMO=1 and an isolated .ci app") }
        _ = NSApplication.shared
        let a=Workspace(name:"first",mode:.general,documents:[ContextDocument(name:"first.md",pages:["Kubernetes 调度器 first workspace"])]),
            b=Workspace(name:"second",mode:.general,documents:[ContextDocument(name:"second.md",pages:["PostgreSQL 查询 second workspace"])])
        WorkspaceStore.shared.installDemo([a,b])
        let state=AppState.shared
        state.runtimeInstalled=true;state.installedDocumentModels=[ModelCatalog.qwen3Embedding.id]
        state.semanticRetrievalEnabled=true;state.rebuildContextIndex()
        let old=state.semanticIndexTask
        try await Task.sleep(for:.milliseconds(100))
        state.switchWorkspace(b.id)
        await old?.value;await state.semanticIndexTask?.value
        guard state.contextIndex.chunks.allSatisfy({ $0.source.contains("second.md") }), !state.contextIndex.chunks.isEmpty,
              !FileManager.default.fileExists(atPath:SemanticIndexStore.url(for:ModelCatalog.qwen3Embedding.id,workspaceID:a.id).path) else { throw SelfTest.Failure("workspace switch allowed stale commit") }
        let before=try await DocumentWorker.embedding.request("health")
        let cached=state.semanticIndex
        state.rebuildContextIndex();await state.semanticIndexTask?.value
        let after=try await DocumentWorker.embedding.request("health")
        guard cached==state.semanticIndex,(before["counters"] as? [String:Int])==after["counters"] as? [String:Int] else { throw SelfTest.Failure("unchanged chunks re-embedded") }
        let docID=b.documents[0].id
        state.setDocumentEnabled(docID,enabled:false);await state.semanticIndexTask?.value
        try await Task.sleep(for:.milliseconds(100))
        guard state.contextIndex.chunks.isEmpty else { throw SelfTest.Failure("disabled document restored by stale task") }
        state.setDocumentEnabled(docID,enabled:true);state.removeDocument(docID)
        await state.semanticIndexTask?.value;try await Task.sleep(for:.milliseconds(100))
        guard state.contextIndex.chunks.isEmpty,state.documents.isEmpty else { throw SelfTest.Failure("removed document restored") }
        await DocumentWorker.embedding.unload()
        return "workspace switch, late index rejection, disabled/deleted documents, revision-bound cache and zero embedding calls on unchanged chunks passed"
    }
    static func modelFiles() async throws -> String {
        let source=ModelCatalog.paddleOCR
        let file=source.files.first { $0.path=="config.json" }!
        let descriptor=ManagedModelDescriptor(id:"selftest-managed-download",name:"selftest",kind:.ocr,repository:source.repository,revision:source.revision,files:[file],summary:"",license:source.license,homepage:source.homepage,note:"",minimumOS:"14")
        defer { try? ManagedModelStore.remove(descriptor) }
        try await ManagedModelStore.download(descriptor, searchLocal: false) { _,_ in }
        guard ManagedModelStore.isInstalled(descriptor) else { throw SelfTest.Failure("download readiness") }
        let url=descriptor.directory.appendingPathComponent(file.path),mtime=try url.resourceValues(forKeys:[.contentModificationDateKey]).contentModificationDate
        try await ManagedModelStore.download(descriptor, searchLocal: false) { _,_ in }
        guard mtime == (try url.resourceValues(forKeys:[.contentModificationDateKey])).contentModificationDate else { throw SelfTest.Failure("verified download file not reused") }
        try Data("broken".utf8).write(to:url)
        guard !ManagedModelStore.isInstalled(descriptor) else { throw SelfTest.Failure("size corruption not detected") }
        try await ManagedModelStore.download(descriptor, searchLocal: false) { _,_ in }
        try ManagedModelStore.verify(descriptor,at:descriptor.directory)
        let large=source.files.first { $0.path=="model.safetensors" }!
        let cancellable=ManagedModelDescriptor(id:"selftest-managed-cancel",name:"cancel",kind:.ocr,repository:source.repository,revision:source.revision,files:[large],summary:"",license:source.license,homepage:source.homepage,note:"",minimumOS:"14")
        defer { try? ManagedModelStore.remove(cancellable) }
        let task=Task { try await ManagedModelStore.download(cancellable, searchLocal: false) { _,_ in } }
        try await Task.sleep(for:.milliseconds(100));task.cancel()
        do { try await task.value;throw SelfTest.Failure("download did not cancel") } catch is CancellationError { }
        try ManagedModelStore.remove(cancellable)
        try await Task.sleep(for:.milliseconds(200))
        guard !FileManager.default.fileExists(atPath:cancellable.directory.path) else { throw SelfTest.Failure("late downloader recreated deleted files") }
        return "real pinned download, verified-file reuse, corruption repair, cancellation and no late writes after delete passed"
    }
}

extension DocumentSelfTest {
    @MainActor static func modelDeletion() async throws -> String {
        let model=ModelCatalog.qwen3Embedding
        guard DemoMode.isEnabled,model.directory.resolvingSymlinksInPath().path.hasPrefix(AppPaths.root.resolvingSymlinksInPath().path+"/") else { throw SelfTest.Failure("model-delete requires DEMO=1 and a disposable model copy inside the isolated data directory") }
        _ = NSApplication.shared
        let document=ContextDocument(name:"deletion-test",pages:Array(repeating:"Kubernetes 调度器在后台为资料生成向量。",count:50))
        let workspace=Workspace(name:"delete model",mode:.general,documents:[document])
        WorkspaceStore.shared.installDemo([workspace])
        let state=AppState.shared
        state.runtimeInstalled=true;state.semanticRetrievalEnabled=true;state.installedDocumentModels=[model.id]
        state.rebuildContextIndex()
        try await Task.sleep(for:.milliseconds(200))
        state.deleteManagedModel(model)
        let deadline=Date().addingTimeInterval(15)
        while FileManager.default.fileExists(atPath:model.directory.path),Date()<deadline { try await Task.sleep(for:.milliseconds(100)) }
        try await Task.sleep(for:.milliseconds(500))
        guard !FileManager.default.fileExists(atPath:model.directory.path), !state.embeddingModelInstalled,
              !FileManager.default.fileExists(atPath:SemanticIndexStore.url(for:model.id,workspaceID:workspace.id).path),!state.isBuildingSemanticIndex else { throw SelfTest.Failure("running model deletion left files, index or ready state") }
        return "running index cancelled, worker unloaded, model and index removed, no late recreation"
    }
}

extension DocumentSelfTest {
    @MainActor static func archiveRoundTrip() async throws -> String {
        let original = Data("authored original-byte backup fixture".utf8)
        let hash = IncrementalBackupStore.digest(original)
        let originalURL = AppPaths.root.appendingPathComponent("Originals/" + hash + ".pdf")
        try FileManager.default.createDirectory(at: originalURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        try original.write(to: originalURL)
        let content=DocumentContent(pipeline:"selftest",sourceHash:hash,blocks:DocumentStructure.parse("## Data\n\n| A | B |\n| --- | --- |\n| exact | 1,234.56 |\n\n$$\nx^2\n$$",locator:.init(page:7)),warnings:["explicit test warning"])
        let document=ContextDocument(name:"structured.pdf",pages:content.blocks.map(\.markdown),markdownPath:"/untrusted/path/original.pdf",sourceFormat:"pdf",content:content)
        let workspace=Workspace(name:"structured backup",mode:.general,documents:[document])
        let store=WorkspaceStore.shared;store.installDemo([workspace])
        let file=AppPaths.root.appendingPathComponent("structured.ihbackup")
        try DataBackup.exportAll(to:file,includeSecrets:false,workspaces:[workspace])
        let listing=try Shell.run("/usr/bin/unzip",["-Z1",file.path]).output
        guard listing.contains("Materials/"),!listing.contains("/models/"),!listing.contains("/Runtimes/") else { throw SelfTest.Failure("backup missing derived Markdown or contains reconstructible assets") }
        store.installDemo([])
        try FileManager.default.removeItem(at: originalURL)
        _ = try await DataBackup.importArchive(file,applySettings:false)
        guard try Data(contentsOf: originalURL) == original else { throw SelfTest.Failure("original source bytes did not restore") }
        guard let restored=store.workspaces.first?.documents.first,restored.content==content,
              let relative=restored.markdownPath,!relative.hasPrefix("/"),let url=MaterialArchive.resolve(relative),
              try String(contentsOf:url,encoding:.utf8).contains("1,234.56"),restored.content?.blocks.first?.locator.page==7 else { throw SelfTest.Failure("structured archive/locators/relative paths did not restore") }
        let damaged = AppPaths.root.appendingPathComponent("damaged-backup", isDirectory: true)
        try FileManager.default.createDirectory(at: damaged, withIntermediateDirectories: true)
        try Shell.check("/usr/bin/ditto", ["-x","-k",file.path,damaged.path], "测试解压")
        let archiveRoot = damaged.appendingPathComponent("InterviewHacker-Backup", isDirectory: true)
        try Data("corrupted original".utf8).write(to: archiveRoot.appendingPathComponent("Originals/" + hash + ".pdf"))
        let broken = AppPaths.root.appendingPathComponent("damaged.ihbackup")
        try Shell.check("/usr/bin/ditto", ["-c","-k","--keepParent",archiveRoot.path,broken.path], "测试压缩")
        let before = store.workspaces
        do { _ = try await DataBackup.importArchive(broken,applySettings:false); throw SelfTest.Failure("corrupt source accepted") }
        catch let failure as SelfTest.Failure { throw failure }
        catch { guard store.workspaces == before else { throw SelfTest.Failure("failed restore changed workspaces") } }
        return "structured Markdown/table/formula/physical locator + original bytes restored; corrupt original rejected before any workspace merge; no models/runtime in archive"
    }
}

extension DocumentSelfTest {
    /// State-only setup checks: no download, online model call, or production data.
    @MainActor static func setupChoices() async throws -> String {
        guard DemoMode.isEnabled, Bundle.main.bundleIdentifier?.hasPrefix("com.interviewhacker.app.ci") == true else {
            throw SelfTest.Failure("document-setup requires INTERVIEWHACKER_DEMO=1 and an isolated com.interviewhacker.app.ci test bundle")
        }
        _ = NSApplication.shared
        let state = AppState.shared
        state.answerBackend = .openAICompatible; state.apiModel = "test-model"; state.apiKey = ""
        state.apiBaseURL = "http://127.0.0.1:9999/v1"
        guard state.answerEngineReady else { throw SelfTest.Failure("local API readiness") }
        state.apiBaseURL = "https://localhost.example.invalid/v1"
        guard !state.answerEngineReady else { throw SelfTest.Failure("remote API bypassed key readiness") }
        state.apiKey = "test-only-not-a-secret"; state.apiBaseURL = ""
        guard !state.answerEngineReady else { throw SelfTest.Failure("missing API address marked ready") }
        state.apiKey = ""; state.answerBackend = .codexAppServer
        state.documentModelsChecked = true; state.runtimeInstalled = true
        state.installedDocumentModels = Set(ModelCatalog.all.map(\.id))
        state.selectDocumentProvider(.system)
        state.prepareDocumentFeature(.paddle)
        await state.documentSetupTask?.value
        guard state.documentOCRProvider == .paddle else { throw SelfTest.Failure("ready bundle did not select provider") }
        state.prepareDocumentFeature(.paddle)
        state.selectDocumentProvider(.system)
        await state.documentSetupTask?.value
        guard state.documentOCRProvider == .system else { throw SelfTest.Failure("late setup overwrote explicit provider choice") }
        state.prepareDocumentFeature(.semantic)
        state.selectSemanticRetrieval(false)
        await state.documentSetupTask?.value
        guard !state.semanticRetrievalEnabled else { throw SelfTest.Failure("late setup overwrote keyword choice") }
        state.prepareDocumentFeature(.paddle)
        state.documentSetupTask?.cancel()
        await state.documentSetupTask?.value
        guard state.documentOCRProvider == .system, state.documentSetupFeature == nil else { throw SelfTest.Failure("cancelled setup selected a provider") }
        let modelBefore = state.codexModelID, providerBefore = state.documentOCRProvider
        state.simplifiedInterface = false; state.saveAnswerSettings()
        guard UserDefaults.standard.bool(forKey: "advancedSettingsEnabled") else { throw SelfTest.Failure("expert preference not persisted") }
        state.simplifiedInterface = true; state.saveAnswerSettings()
        guard !UserDefaults.standard.bool(forKey: "advancedSettingsEnabled"), state.codexModelID == modelBefore, state.documentOCRProvider == providerBefore else { throw SelfTest.Failure("hiding expert options modified configuration") }
        let defaults = UserDefaults.standard
        defaults.set("0.0", forKey: "onboardingGuideStartedVersion")
        defaults.set("0.0", forKey: "onboardingGuideCompletedVersion")
        defaults.set(true, forKey: "onboardingGuideEnabled")
        defaults.removeObject(forKey: "onboardingGuideDismissedVersion")
        defaults.set(3, forKey: "quickStartStep")
        state.prepareOnboardingForCurrentVersion()
        guard state.isOnboardingPresented, defaults.integer(forKey: "quickStartStep") == 0 else { throw SelfTest.Failure("feature upgrade did not restart guide") }
        defaults.set(2, forKey: "quickStartStep")
        state.isOnboardingPresented = false
        state.prepareOnboardingForCurrentVersion()
        guard state.isOnboardingPresented, defaults.integer(forKey: "quickStartStep") == 2 else { throw SelfTest.Failure("interrupted guide did not resume") }
        state.setOnboardingGuideEnabled(false)
        guard !state.isOnboardingPresented, !state.onboardingGuideEnabled,
              !defaults.bool(forKey: "onboardingGuideEnabled"), defaults.integer(forKey: "quickStartStep") == 2 else { throw SelfTest.Failure("disabling guide did not preserve its progress") }
        state.setOnboardingGuideEnabled(true)
        guard state.isOnboardingPresented, state.onboardingGuideEnabled,
              defaults.integer(forKey: "quickStartStep") == 2 else { throw SelfTest.Failure("re-enabling guide did not resume its progress") }
        state.finishOnboarding(); state.prepareOnboardingForCurrentVersion()
        guard !state.isOnboardingPresented else { throw SelfTest.Failure("completed guide repeated") }
        state.openOnboarding()
        guard state.isOnboardingPresented, defaults.integer(forKey: "quickStartStep") == 0 else { throw SelfTest.Failure("manual guide restart failed") }
        state.semanticIndexTask?.cancel()
        return "ready bundle reuse, cancellation, late-choice protection, version restart, resume, completion and advanced-mode persistence passed; no network/model calls"
    }
}


extension DocumentSelfTest {
    /// Explicit local integration test; never downloads or mutates the source directories.
    static func localModels(_ options: [String: String]) async throws -> String {
        guard let input = options["input"], let archiveDirectory = options["archive-directory"] else {
            throw SelfTest.Failure("local-models requires --input model-root --archive-directory runtime-archive-root; uses isolated data directory")
        }
        let sources = [URL(fileURLWithPath: input), URL(fileURLWithPath: archiveDirectory)]
        guard !ModelCatalog.all.contains(where: { $0.directory.standardizedFileURL.path.hasPrefix(sources[0].standardizedFileURL.path + "/") }) else {
            throw SelfTest.Failure("destination must differ from input")
        }
        let result = try await LocalDocumentAssets.reuse(models: ModelCatalog.all, runtime: true, searchRoots: sources)
        for descriptor in ModelCatalog.all {
            guard ManagedModelStore.isInstalled(descriptor) else { throw SelfTest.Failure("local model not ready: \(descriptor.id)") }
            try ManagedModelStore.verify(descriptor, at: descriptor.directory)
        }
        guard DocumentRuntime.installed else { throw SelfTest.Failure("local runtime missing") }
        let health = try await DocumentWorker.shared.request("health")
        guard health["protocol"] as? Int == 1 else { throw SelfTest.Failure("reused runtime health") }
        await DocumentWorker.shared.unload()
        let second = try await LocalDocumentAssets.reuse(models: ModelCatalog.all, runtime: true, searchRoots: sources)
        guard second.reusedBytes == 0, !second.runtime else { throw SelfTest.Failure("repeat import did not reuse installed state") }
        return "3 pinned models + runtime ready; reused \(result.reusedBytes) model bytes; NDJSON health; repeat scan skipped installed assets; no network"
    }
}


extension DocumentSelfTest {
    @MainActor static func modelReadiness() async throws -> String {
        guard DemoMode.isEnabled, Bundle.main.bundleIdentifier?.hasPrefix("com.interviewhacker.app.ci") == true else {
            throw SelfTest.Failure("model-readiness requires DEMO=1 and an isolated .ci test bundle; pass real model directory explicitly")
        }
        let state = AppState.shared
        state.documentModelsChecked = false
        let checking = Task { await state.refreshManagedModelStatus() }
        try await Task.sleep(for: .milliseconds(50))
        guard !state.documentModelsChecked else { throw SelfTest.Failure("requires real model weights to overlap verification with speech refresh") }
        state.modelInstallRevision += 1 // Concurrent speech-model refresh must not invalidate document verification.
        await checking.value
        guard state.documentModelsChecked, state.installedDocumentModels.count == ModelCatalog.all.count else {
            throw SelfTest.Failure("speech refresh discarded document readiness")
        }
        state.semanticIndexTask?.cancel()
        return "real document hash verification survives concurrent speech-model revision; all 3 models ready"
    }
}
