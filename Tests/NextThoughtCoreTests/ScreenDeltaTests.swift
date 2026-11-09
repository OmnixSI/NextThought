import Testing
@testable import NextThoughtCore
@Suite struct ScreenDeltaTests {
    @Test func initialUnchangedAndLocalizedCrop() {
        let w = 100, h = 100, empty = [UInt8](repeating: 0,count: w*h*4)
        #expect(ScreenDelta.region(previous: nil,current: empty,width: w,height: h) == [0,0,1,1])
        #expect(ScreenDelta.region(previous: empty,current: empty,width: w,height: h) == nil)
        var changed = empty
        for y in 20..<30 { for x in 60..<75 { changed[(y*w+x)*4] = 255 } }
        let region = ScreenDelta.region(previous: empty,current: changed,width: w,height: h)!
        #expect(region[0] <= 0.6 && region[0]+region[2] >= 0.75)
        #expect(region[1] <= 0.2 && region[1]+region[3] >= 0.3)
        #expect(region[2]*region[3] < 0.1)
    }
}
