import SwiftUI
import NextThoughtCore

/// 观点对照: the topic, a matrix (rows are issues, columns are the sides), then what was
/// agreed, where people still differ and who does what. Click a time to jump to the transcript.
struct ViewpointMatrixView: View {
    let matrix: ViewpointMatrix
    let onJump: (Int) -> Void

    private static let issueWidth: CGFloat = 96
    private static let cellWidth: CGFloat = 170

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s10) {
            if !matrix.topic.isEmpty { topicBlock }
            if !matrix.issues.isEmpty && !matrix.parties.isEmpty { grid }
            if !matrix.consensus.isEmpty { list(title: "共识", icon: "checkmark", items: matrix.consensus) }
            if !matrix.disagreements.isEmpty { list(title: "分歧", icon: "arrow.left.arrow.right", items: matrix.disagreements) }
            if !matrix.owners.isEmpty { ownersView }
        }
    }

    // MARK: Topic

    private var topicBlock: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            AIHeading(title: "议题")
            HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                Text(verbatim: matrix.topic).font(Typo.body).lineSpacing(6).textSelection(.enabled)
                StructuredTimeLink(at: matrix.topicAt, onJump: onJump)
            }
        }
        .aiBlock(padding: Space.s6)
    }

    // MARK: Matrix

    private var grid: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            Grid(alignment: .topLeading, horizontalSpacing: Space.s4, verticalSpacing: 0) {
                GridRow {
                    Color.clear.frame(width: Self.issueWidth, height: 1)
                    ForEach(matrix.parties, id: \.self) { party in
                        Text(verbatim: party)
                            .font(Typo.label)
                            .lineLimit(1).truncationMode(.tail)
                            .frame(width: Self.cellWidth, alignment: .leading)
                            .padding(.bottom, Space.s2)
                    }
                }
                ForEach(Array(matrix.issues.enumerated()), id: \.offset) { _, issue in
                    Rectangle().fill(Ink.hairline).frame(height: 1)
                    GridRow(alignment: .top) {
                        Text(verbatim: issue.title)
                            .font(.zoomed(size: 14, weight: .semibold))
                            .frame(width: Self.issueWidth, alignment: .topLeading)
                            .padding(.vertical, Space.s3)
                        ForEach(matrix.parties, id: \.self) { party in
                            cell(matrix.positions(issue: issue, party: party))
                        }
                    }
                }
            }
        }
    }

    private func cell(_ positions: [ViewpointPosition]) -> some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            if positions.isEmpty {
                Text(verbatim: "—").font(Typo.body).foregroundStyle(Ink.tertiary)
            }
            ForEach(Array(positions.enumerated()), id: \.offset) { _, position in
                VStack(alignment: .leading, spacing: 2) {
                    Text(verbatim: position.text).font(Typo.body).lineSpacing(4).textSelection(.enabled)
                    StructuredTimeLink(at: position.at, onJump: onJump)
                }
            }
        }
        .frame(width: Self.cellWidth, alignment: .topLeading)
        .padding(.vertical, Space.s3)
    }

    // MARK: Lists

    private func list(title: LocalizedStringKey, icon: String, items: [TimedText]) -> some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            Text(title).font(Typo.section)
            ForEach(Array(items.enumerated()), id: \.offset) { _, item in
                HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                    Image(systemName: icon).font(.zoomed(size: 10, weight: .medium)).foregroundStyle(Ink.secondary)
                    Text(verbatim: item.text).font(Typo.body).lineSpacing(5).textSelection(.enabled)
                    StructuredTimeLink(at: item.at, onJump: onJump)
                }
            }
        }
    }

    private var ownersView: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("分工").font(Typo.section).padding(.bottom, Space.s3)
            ForEach(Array(matrix.owners.enumerated()), id: \.offset) { _, owner in
                Rectangle().fill(Ink.hairline).frame(height: 1)
                HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                    HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                        Text(verbatim: owner.task).font(Typo.body).textSelection(.enabled)
                        StructuredTimeLink(at: owner.at, onJump: onJump)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    Text(verbatim: owner.who.isEmpty ? "—" : owner.who).font(Typo.body).foregroundStyle(Ink.secondary)
                        .frame(width: 72, alignment: .leading)
                    Text(verbatim: owner.due ?? "—").font(Typo.body).foregroundStyle(Ink.secondary)
                        .frame(width: 120, alignment: .leading)
                }
                .padding(.vertical, Space.s3)
            }
        }
    }
}
