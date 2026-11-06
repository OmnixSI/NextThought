import Testing
@testable import NextThoughtCore
@Suite struct BackupArchivePolicyTests {
    @Test func portableArchiveAndResourceGuard() throws {
        let names = "Backup/\nBackup/manifest.json\n", listing = "drwxr-xr-x folder\n-rw-r--r-- manifest\n2 files, 100 bytes uncompressed, 50 bytes compressed"
        try BackupArchivePolicy.validate(names:names,listing:listing,freeBytes:1024*1024*1024)
        #expect(throws: (any Error).self) { try BackupArchivePolicy.validate(names:names,listing:listing,freeBytes:100) }
        #expect(throws: (any Error).self) { try BackupArchivePolicy.validate(names:"Backup/../escape\n",listing:"1 file, 1 bytes uncompressed",freeBytes:1024*1024*1024) }
        #expect(throws: (any Error).self) { try BackupArchivePolicy.validate(names:"Backup/link\n",listing:"lrwxrwxrwx link\n1 file, 1 bytes uncompressed",freeBytes:1024*1024*1024) }
    }
}
