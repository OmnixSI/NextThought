import SwiftUI

/// Places each child at its natural size and starts a new row when the available
/// width is full. Measurement and placement use the same row calculation.
struct WrappingHStack: Layout {
    var spacing: CGFloat = 8
    var lineSpacing: CGFloat = 6

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let limit = Self.widthLimit(proposal.width)
        let rows = rows(subviews: subviews, maxWidth: limit)
        let contentWidth = rows.map(\.width).max() ?? 0
        let width = limit.isFinite ? limit : contentWidth
        let height = rows.reduce(0) { $0 + $1.height }
            + Self.validSpacing(lineSpacing) * CGFloat(max(0, rows.count - 1))
        return CGSize(width: width, height: height)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        let rows = rows(subviews: subviews, maxWidth: Self.widthLimit(bounds.width))
        var y = bounds.minY
        for row in rows {
            var x = bounds.minX
            for item in row.items {
                subviews[item.index].place(
                    at: CGPoint(x: x, y: y + (row.height - item.size.height) / 2),
                    anchor: .topLeading,
                    proposal: ProposedViewSize(item.size)
                )
                x += item.size.width + Self.validSpacing(spacing)
            }
            y += row.height + Self.validSpacing(lineSpacing)
        }
    }

    private struct Item {
        var index: Int
        var size: CGSize
    }

    private struct Row {
        var items: [Item] = []
        var width: CGFloat = 0
        var height: CGFloat = 0
    }

    private func rows(subviews: Subviews, maxWidth: CGFloat) -> [Row] {
        var result: [Row] = []
        var current = Row()
        let itemSpacing = Self.validSpacing(spacing)

        for index in subviews.indices {
            let size = subviews[index].sizeThatFits(.unspecified)
            let needed = current.items.isEmpty ? size.width : current.width + itemSpacing + size.width
            if needed > maxWidth, !current.items.isEmpty {
                result.append(current)
                current = Row()
            }

            let rowWidth = current.items.isEmpty ? size.width : current.width + itemSpacing + size.width
            current.items.append(Item(index: index, size: size))
            current.width = rowWidth
            current.height = max(current.height, size.height)
        }

        if !current.items.isEmpty { result.append(current) }
        return result
    }

    private static func widthLimit(_ proposed: CGFloat?) -> CGFloat {
        guard let proposed, proposed.isFinite else { return .infinity }
        return max(0, proposed)
    }

    private static func validSpacing(_ value: CGFloat) -> CGFloat {
        value.isFinite ? max(0, value) : 0
    }
}
