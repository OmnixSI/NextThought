import SwiftUI
import NextThoughtCore

/// 因果鱼骨图: the problem is the head on the right, the cause categories are the big bones
/// (alternating above and below the spine, slanting toward the head) and each cause is a
/// small horizontal bone with its text on top. Click a cause to jump to the transcript at
/// the time it was said. Narrow widths fall back to a list that folds per category.
///
/// Text rules: a cause shows at most three lines and is cut with "…" after that (the tooltip
/// has the whole sentence); a category name is one line; the head shows at most four lines.
struct FishboneView: View {
    let notes: FishboneNotes
    let onJump: (Int) -> Void
    @State private var width: CGFloat = 0

    var body: some View {
        content
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(GeometryReader { proxy in
                Color.clear
                    .onAppear { width = proxy.size.width }
                    .onChange(of: proxy.size.width) { _, newWidth in width = newWidth }
            })
            .paperCard()
    }

    @ViewBuilder private var content: some View {
        if let geometry = FishboneGeometry.make(head: notes.head, categories: notes.categories, width: width) {
            FishboneDiagram(notes: notes, geometry: geometry, onJump: onJump)
        } else {
            FishboneTree(notes: notes, onJump: onJump)
        }
    }
}

// MARK: - Geometry

/// Where everything of a fishbone sits for a given width. Text is measured by a simple
/// estimate (a wide character is one em, others about 0.56 em), so rows get a fixed number
/// of lines and nothing depends on how SwiftUI wraps.
struct FishboneGeometry {
    struct Segment { var from: CGPoint; var to: CGPoint }
    struct Row { var category: Int; var cause: Int; var frame: CGRect; var lines: Int }
    struct Tag { var category: Int; var frame: CGRect }

    var size: CGSize
    var spine: Segment
    var head: CGRect
    var bones: [Segment]
    var ticks: [Segment]
    var rows: [Row]
    var labels: [Tag]

    static let lineHeight: CGFloat = 16
    static let labelHeight: CGFloat = 24
    static let slant: CGFloat = 34
    static let minColumnWidth: CGFloat = 150
    static let causeFont: CGFloat = 12

    /// Estimated number of lines `text` needs at `width`, between 1 and `maxLines`.
    static func lines(_ text: String, width: CGFloat, fontSize: CGFloat, maxLines: Int) -> Int {
        let total = textWidth(text, fontSize: fontSize)
        let usable = max(1, width * 0.88)
        return min(maxLines, max(1, Int((total / usable).rounded(.up))))
    }

    static func textWidth(_ text: String, fontSize: CGFloat) -> CGFloat {
        var total: CGFloat = 0
        for scalar in text.unicodeScalars { total += scalar.value >= 0x2E80 ? fontSize : fontSize * 0.56 }
        return total
    }

    /// nil when the width is too small for a diagram (the caller draws a list instead).
    static func make(head: String, categories: [FishboneCategory], width: CGFloat) -> FishboneGeometry? {
        let count = categories.count
        guard count > 0, width > 0 else { return nil }
        let columns = (count + 1) / 2
        let headWidth = min(160, max(112, width * 0.2))
        let columnWidth = (width - headWidth - 14) / CGFloat(columns)
        guard columnWidth >= minColumnWidth else { return nil }
        let rowTextWidth = columnWidth - slant - 12

        // Row heights per category: lines of text, then 10 pt for the tick line and air.
        let lineCounts: [[Int]] = categories.map { category in
            category.causes.map { lines($0.text, width: rowTextWidth, fontSize: causeFont, maxLines: 3) }
        }
        let heights: [[CGFloat]] = lineCounts.map { counts in counts.map { CGFloat($0) * lineHeight + 10 } }
        var upperRows: CGFloat = 0
        var lowerRows: CGFloat = 0
        for (index, rowHeights) in heights.enumerated() {
            let total = rowHeights.reduce(0, +)
            if index % 2 == 0 { upperRows = max(upperRows, total) } else { lowerRows = max(lowerRows, total) }
        }
        let hasLower = count > 1
        let upperBone = max(48, upperRows + 10)
        let lowerBone = hasLower ? max(48, lowerRows + 10) : 0
        let boneTop = labelHeight + 2
        let spineY = boneTop + upperBone

        let headLines = lines(head, width: headWidth - 24, fontSize: 13, maxLines: 4)
        let headHeight = CGFloat(headLines) * 18 + 24
        let below = max(hasLower ? lowerBone + labelHeight + 2 : 0, headHeight / 2 + 8)
        let height = spineY + below

        var bones: [Segment] = []
        var ticks: [Segment] = []
        var rows: [Row] = []
        var labels: [Tag] = []
        for (index, category) in categories.enumerated() {
            let upper = index % 2 == 0
            let x0 = CGFloat(index / 2) * columnWidth
            let xEnd = x0 + columnWidth - 4
            let xTop = xEnd - slant
            let boneLength = upper ? upperBone : lowerBone
            // x of the big bone at height y.
            func boneX(_ y: CGFloat) -> CGFloat {
                upper ? xTop + slant * (y - boneTop) / boneLength : xEnd - slant * (y - spineY) / boneLength
            }
            bones.append(upper ? Segment(from: CGPoint(x: xTop, y: boneTop), to: CGPoint(x: xEnd, y: spineY))
                               : Segment(from: CGPoint(x: xTop, y: spineY + boneLength), to: CGPoint(x: xEnd, y: spineY)))

            var y = (upper ? boneTop : spineY) + 6
            for (cause, rowLines) in lineCounts[index].enumerated() {
                let textHeight = CGFloat(rowLines) * lineHeight
                rows.append(Row(category: index, cause: cause, frame: CGRect(x: x0, y: y, width: rowTextWidth, height: textHeight), lines: rowLines))
                let tickY = y + textHeight + 3
                ticks.append(Segment(from: CGPoint(x: x0, y: tickY), to: CGPoint(x: boneX(tickY), y: tickY)))
                y += textHeight + 10
            }

            let labelWidth = min(columnWidth - 8, max(48, textWidth(category.name, fontSize: 12) + 24))
            let labelX = min(max(xTop - labelWidth / 2, x0), x0 + columnWidth - labelWidth - 4)
            let labelY = upper ? 0 : spineY + boneLength + 2
            labels.append(Tag(category: index, frame: CGRect(x: labelX, y: labelY, width: labelWidth, height: labelHeight)))
        }

        let headFrame = CGRect(x: width - headWidth, y: spineY - headHeight / 2, width: headWidth, height: headHeight)
        return FishboneGeometry(size: CGSize(width: width, height: height),
                                spine: Segment(from: CGPoint(x: 0, y: spineY), to: CGPoint(x: headFrame.minX, y: spineY)),
                                head: headFrame, bones: bones, ticks: ticks, rows: rows, labels: labels)
    }
}

// MARK: - Diagram

private struct Segments: Shape {
    var segments: [FishboneGeometry.Segment]
    func path(in rect: CGRect) -> Path {
        var path = Path()
        for segment in segments {
            path.move(to: segment.from)
            path.addLine(to: segment.to)
        }
        return path
    }
}

private struct FishboneDiagram: View {
    let notes: FishboneNotes
    let geometry: FishboneGeometry
    let onJump: (Int) -> Void

    var body: some View {
        ZStack(alignment: .topLeading) {
            Segments(segments: geometry.ticks).stroke(Ink.tertiary.opacity(0.7), lineWidth: 1)
            Segments(segments: geometry.bones).stroke(Ink.tertiary, style: StrokeStyle(lineWidth: 1.2, lineCap: .round))
            Segments(segments: [geometry.spine]).stroke(Ink.secondary, style: StrokeStyle(lineWidth: 1.6, lineCap: .round))
            ForEach(geometry.rows.indices, id: \.self) { index in
                causeRow(geometry.rows[index])
            }
            ForEach(geometry.labels.indices, id: \.self) { index in
                categoryLabel(geometry.labels[index])
            }
            headBox
        }
        .frame(width: geometry.size.width, height: geometry.size.height, alignment: .topLeading)
    }

    private func causeRow(_ row: FishboneGeometry.Row) -> some View {
        let cause = notes.categories[row.category].causes[row.cause]
        return Button {
            if let at = cause.at { onJump(at) }
        } label: {
            Text(verbatim: cause.text)
                .font(.system(size: FishboneGeometry.causeFont))
                .foregroundStyle(Ink.ink)
                .multilineTextAlignment(.leading)
                .lineLimit(row.lines)
                .frame(width: row.frame.width, height: row.frame.height, alignment: .topLeading)
        }
        .buttonStyle(InteractiveRowButtonStyle())
        .disabled(cause.at == nil)
        .help(Self.tooltip(cause))
        .offset(x: row.frame.minX, y: row.frame.minY)
    }

    private func categoryLabel(_ label: FishboneGeometry.Tag) -> some View {
        let category = notes.categories[label.category]
        let earliest = category.causes.compactMap(\.at).min()
        return Button {
            if let earliest { onJump(earliest) }
        } label: {
            Text(verbatim: category.name)
                .font(.system(size: 12, weight: .semibold))
                .foregroundStyle(Ink.ink)
                .lineLimit(1).truncationMode(.tail)
                .padding(.horizontal, Space.s2)
                .frame(width: label.frame.width, height: label.frame.height)
                .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
        }
        .buttonStyle(.plain)
        .disabled(earliest == nil)
        .help(category.name)
        .offset(x: label.frame.minX, y: label.frame.minY)
    }

    private var headBox: some View {
        let frame = geometry.head
        return Button {
            if let at = notes.headAt { onJump(at) }
        } label: {
            Text(verbatim: notes.head.isEmpty ? String(localized: "议题") : notes.head)
                .font(.system(size: 13, weight: .semibold))
                .foregroundStyle(Ink.ink)
                .multilineTextAlignment(.center)
                .lineLimit(4)
                .padding(.horizontal, Space.s3)
                .frame(width: frame.width, height: frame.height)
                .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
                .overlay(RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous).strokeBorder(Ink.ink, lineWidth: 1.2))
        }
        .buttonStyle(.plain)
        .disabled(notes.headAt == nil)
        .help(notes.head)
        .offset(x: frame.minX, y: frame.minY)
    }

    /// The whole sentence and its time, for the hover tooltip.
    static func tooltip(_ cause: FishboneCause) -> String {
        guard let at = cause.at else { return cause.text }
        return cause.text + " [" + NotesTime.text(at) + "]"
    }
}

// MARK: - List for narrow widths

private struct FishboneTree: View {
    let notes: FishboneNotes
    let onJump: (Int) -> Void
    @State private var folded: Set<Int> = []

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                Text(verbatim: notes.head.isEmpty ? String(localized: "议题") : notes.head)
                    .font(.system(size: 15, weight: .semibold))
                    .textSelection(.enabled)
                StructuredTimeLink(at: notes.headAt, onJump: onJump)
            }
            ForEach(Array(notes.categories.enumerated()), id: \.offset) { index, category in
                VStack(alignment: .leading, spacing: Space.s2) {
                    Button { toggle(index) } label: {
                        HStack(spacing: Space.s2) {
                            Image(systemName: "chevron.right")
                                .font(.system(size: 9, weight: .semibold))
                                .foregroundStyle(Ink.secondary)
                                .rotationEffect(.degrees(folded.contains(index) ? 0 : 90))
                            Text(verbatim: category.name).font(Typo.label).foregroundStyle(Ink.ink)
                            Text(verbatim: "\(category.causes.count)").font(Typo.caption).foregroundStyle(Ink.secondary)
                            Spacer(minLength: 0)
                        }
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                    if !folded.contains(index) {
                        VStack(alignment: .leading, spacing: Space.s2) {
                            ForEach(Array(category.causes.enumerated()), id: \.offset) { _, cause in
                                HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                                    Text(verbatim: cause.text).font(Typo.body).lineSpacing(4).textSelection(.enabled)
                                    StructuredTimeLink(at: cause.at, onJump: onJump)
                                }
                            }
                        }
                        .padding(.leading, Space.s4)
                        .overlay(alignment: .leading) { Rectangle().fill(Ink.hairline).frame(width: 1) }
                    }
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func toggle(_ index: Int) {
        withAnimation(Motion.expand) {
            if folded.contains(index) { folded.remove(index) } else { folded.insert(index) }
        }
    }
}
