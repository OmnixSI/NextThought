import Foundation
enum AppFailure: Error { case transcription(String), codexUnavailable, codexNotSignedIn, capture(String) }
enum Diagnostics {
 static let lock = NSLock()
 static var readyCount = 0
 static func record(_ event: String, _ fields: [String: Any] = [:]) {
  if event == "appserver.ready" { lock.withLock { readyCount += 1 } }
 }
}
@main struct Probe {
 static func main() async throws {
  setvbuf(stdout, nil, _IONBF, 0)
  let client = CodexAppServerClient()
  defer { client.shutdown() }
  async let warmA: Void = client.prewarm()
  async let warmB: Void = client.prewarm()
  _ = await (warmA, warmB)
  let second = CodexAppServerClient()
  defer { second.shutdown() }
  await second.prewarm()
  precondition(Diagnostics.lock.withLock { Diagnostics.readyCount } == 2)
  print("PASS: concurrent prewarm shares startup; idle first connection does not block second")
  var request = AnswerRequest(system: "", user: "test", model: "slow-thread", effort: "low")
  let slow = Task { try await client.answer(request) { _ in } }
  try await Task.sleep(for: .milliseconds(50))
  let started = Date()
  slow.cancel()
  do { _ = try await slow.value; fatalError("did not cancel") } catch is CancellationError {}
  precondition(Date().timeIntervalSince(started) < 1)
  print("PASS: cancellation interrupts pending thread/start immediately")
  request.model = "fast"
  let late = Task { try await client.answer(request) { _ in } }
  try await Task.sleep(for: .milliseconds(50));late.cancel()
  do { _ = try await late.value; fatalError("did not cancel") } catch is CancellationError {}
  try await Task.sleep(for: .milliseconds(250))
  let log = ProcessInfo.processInfo.environment["PROBE_INTERRUPT_LOG"]!
  precondition((try? String(contentsOfFile: log, encoding: .utf8).isEmpty) == false)
  print("PASS: late turn/start acknowledgement is interrupted after cancellation")
  let next = try await client.answer(request) { _ in }
  precondition(next == "done")
  print("PASS: next request succeeds and ignores canceled turn notifications")
 }
}
