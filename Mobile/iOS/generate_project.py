#!/usr/bin/env python3
"""Deterministic, dependency-free Xcode project generation; run from any cwd."""
from pathlib import Path
root=Path(__file__).resolve().parent
project=root/'InterviewHackerCompanion.xcodeproj';project.mkdir(exist_ok=True)
(project/'project.pbxproj').write_text('''// !$*UTF8*$!
{ archiveVersion = 1; classes = {}; objectVersion = 60; objects = {
 A00000000000000000000001 = {isa = PBXProject; buildConfigurationList = A00000000000000000000002; compatibilityVersion = "Xcode 15.0"; developmentRegion = en; knownRegions = (en,Base); mainGroup = A00000000000000000000003; productRefGroup = A00000000000000000000004; projectDirPath = ""; projectRoot = ""; packageReferences = (A00000000000000000000020); targets = (A00000000000000000000005); };
 A00000000000000000000002 = {isa = XCConfigurationList; buildConfigurations = (A00000000000000000000011,A00000000000000000000012); defaultConfigurationIsVisible = 0; defaultConfigurationName = Release; };
 A00000000000000000000003 = {isa = PBXGroup; children = (A00000000000000000000006,A00000000000000000000007,A00000000000000000000004); sourceTree = "<group>"; };
 A00000000000000000000004 = {isa = PBXGroup; children = (A00000000000000000000008); name = Products; sourceTree = "<group>"; };
 A00000000000000000000005 = {isa = PBXNativeTarget; buildConfigurationList = A00000000000000000000010; buildPhases = (A00000000000000000000009,A0000000000000000000000A,A0000000000000000000000B); buildRules = (); dependencies = (); name = InterviewHackerCompanion; packageProductDependencies = (A00000000000000000000021); productName = InterviewHackerCompanion; productReference = A00000000000000000000008; productType = "com.apple.product-type.application"; };
 A00000000000000000000006 = {isa = PBXFileReference; lastKnownFileType = sourcecode.swift; path = InterviewHackerCompanion/App.swift; sourceTree = "<group>"; };
 A00000000000000000000007 = {isa = PBXFileReference; lastKnownFileType = sourcecode.swift; path = InterviewHackerCompanion/QRScanner.swift; sourceTree = "<group>"; };
 A00000000000000000000008 = {isa = PBXFileReference; explicitFileType = wrapper.application; includeInIndex = 0; path = InterviewHackerCompanion.app; sourceTree = BUILT_PRODUCTS_DIR; };
 A00000000000000000000009 = {isa = PBXSourcesBuildPhase; buildActionMask = 2147483647; files = (A0000000000000000000000C,A0000000000000000000000D); runOnlyForDeploymentPostprocessing = 0; };
 A0000000000000000000000A = {isa = PBXFrameworksBuildPhase; buildActionMask = 2147483647; files = (A00000000000000000000022); runOnlyForDeploymentPostprocessing = 0; };
 A0000000000000000000000B = {isa = PBXResourcesBuildPhase; buildActionMask = 2147483647; files = (); runOnlyForDeploymentPostprocessing = 0; };
 A0000000000000000000000C = {isa = PBXBuildFile; fileRef = A00000000000000000000006; };
 A0000000000000000000000D = {isa = PBXBuildFile; fileRef = A00000000000000000000007; };
 A00000000000000000000010 = {isa = XCConfigurationList; buildConfigurations = (A00000000000000000000013,A00000000000000000000014); defaultConfigurationIsVisible = 0; defaultConfigurationName = Release; };
 A00000000000000000000011 = {isa = XCBuildConfiguration; name = Debug; buildSettings = {SDKROOT = iphoneos; IPHONEOS_DEPLOYMENT_TARGET = 16.0; SWIFT_VERSION = 5.0; }; };
 A00000000000000000000012 = {isa = XCBuildConfiguration; name = Release; buildSettings = {SDKROOT = iphoneos; IPHONEOS_DEPLOYMENT_TARGET = 16.0; SWIFT_VERSION = 5.0; }; };
 A00000000000000000000013 = {isa = XCBuildConfiguration; name = Debug; buildSettings = {INFOPLIST_FILE = InterviewHackerCompanion/Info.plist; PRODUCT_BUNDLE_IDENTIFIER = com.interviewhacker.companion; PRODUCT_NAME = "$(TARGET_NAME)"; TARGETED_DEVICE_FAMILY = "1,2"; CODE_SIGN_STYLE = Automatic; SWIFT_OPTIMIZATION_LEVEL = "-Onone"; }; };
 A00000000000000000000014 = {isa = XCBuildConfiguration; name = Release; buildSettings = {INFOPLIST_FILE = InterviewHackerCompanion/Info.plist; PRODUCT_BUNDLE_IDENTIFIER = com.interviewhacker.companion; PRODUCT_NAME = "$(TARGET_NAME)"; TARGETED_DEVICE_FAMILY = "1,2"; CODE_SIGN_STYLE = Automatic; SWIFT_OPTIMIZATION_LEVEL = "-O"; }; };
 A00000000000000000000020 = {isa = XCLocalSwiftPackageReference; relativePath = ../Shared; };
 A00000000000000000000021 = {isa = XCSwiftPackageProductDependency; package = A00000000000000000000020; productName = CompanionMobileCore; };
 A00000000000000000000022 = {isa = PBXBuildFile; productRef = A00000000000000000000021; };
}; rootObject = A00000000000000000000001; }
''')
shared=project/'xcshareddata/xcschemes';shared.mkdir(parents=True,exist_ok=True)
(shared/'InterviewHackerCompanion.xcscheme').write_text('''<?xml version="1.0" encoding="UTF-8"?>
<Scheme LastUpgradeVersion="1600" version="1.7"><BuildAction parallelizeBuildables="YES" buildImplicitDependencies="YES"><BuildActionEntries><BuildActionEntry buildForTesting="YES" buildForRunning="YES" buildForProfiling="YES" buildForArchiving="YES" buildForAnalyzing="YES"><BuildableReference BuildableIdentifier="primary" BlueprintIdentifier="A00000000000000000000005" BuildableName="InterviewHackerCompanion.app" BlueprintName="InterviewHackerCompanion" ReferencedContainer="container:InterviewHackerCompanion.xcodeproj"/></BuildActionEntry></BuildActionEntries></BuildAction><LaunchAction buildConfiguration="Debug" selectedDebuggerIdentifier="Xcode.DebuggerFoundation.Debugger.LLDB" selectedLauncherIdentifier="Xcode.IDEFoundation.Launcher.LLDB" launchStyle="0" useCustomWorkingDirectory="NO" ignoresPersistentStateOnLaunch="NO" debugDocumentVersioning="YES" debugServiceExtension="internal" allowLocationSimulation="YES"><BuildableProductRunnable runnableDebuggingMode="0"><BuildableReference BuildableIdentifier="primary" BlueprintIdentifier="A00000000000000000000005" BuildableName="InterviewHackerCompanion.app" BlueprintName="InterviewHackerCompanion" ReferencedContainer="container:InterviewHackerCompanion.xcodeproj"/></BuildableProductRunnable></LaunchAction><ArchiveAction buildConfiguration="Release" revealArchiveInOrganizer="YES"/></Scheme>
''')
print(project)
