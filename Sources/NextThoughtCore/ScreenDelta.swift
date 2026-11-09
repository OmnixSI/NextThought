import Foundation

/// Bounding region of meaningful changed pixels in two small RGBA previews.
/// Coordinates are normalized in the preview's top-left pixel coordinate space.
public enum ScreenDelta {
    public static func region(previous: [UInt8]?, current: [UInt8], width: Int, height: Int) -> [Double]? {
        guard width > 0, height > 0, current.count == width * height * 4 else { return nil }
        guard let previous, previous.count == current.count else { return [0,0,1,1] }
        var minX = width, minY = height, maxX = -1, maxY = -1, changed = 0
        for y in 0..<height { for x in 0..<width {
            let offset = (y * width + x) * 4
            if (0..<3).contains(where: { abs(Int(current[offset + $0]) - Int(previous[offset + $0])) >= 12 }) {
                changed += 1; minX = min(minX,x); minY = min(minY,y); maxX = max(maxX,x); maxY = max(maxY,y)
            }
        } }
        guard changed >= max(1,width * height / 2000) else { return nil }
        let margin = max(2,min(width,height) / 30)
        minX = max(0,minX-margin); minY = max(0,minY-margin); maxX = min(width-1,maxX+margin); maxY = min(height-1,maxY+margin)
        let w = Double(maxX-minX+1)/Double(width), h = Double(maxY-minY+1)/Double(height)
        if w*h > 0.75 { return [0,0,1,1] }
        return [Double(minX)/Double(width),Double(minY)/Double(height),w,h]
    }
}
