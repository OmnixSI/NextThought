import SwiftUI
import InterviewHackerCore

/// 要点大纲: numbered sections, their points and sub-points, then the key concepts and what
/// to review. Every line has its time; click it to jump to the transcript.
struct OutlineView: View {
    let notes: OutlineNotes
    let onJump: (Int) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s10) {
            ForEach(Array(notes.sections.enumerated()), id: \.offset) { index, section in
                sectionView(index: index, section: section)
            }
            if !notes.concepts.isEmpty { conceptsView }
            if !notes.review.isEmpty { reviewView }
        }
    }

    // MARK: Sections

    private func sectionView(index: Int, section: OutlineSection) -> some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                Text(verbatim: "\(index + 1)")
                    .font(.system(size: 13, design: .serif)).foregroundStyle(Ink.tertiary)
                    .frame(width: 24, alignment: .leading)
                Text(verbatim: section.title).font(Typo.section).textSelection(.enabled)
                StructuredTimeLink(at: section.at, onJump: onJump)
            }
            VStack(alignment: .leading, spacing: Space.s2) {
                ForEach(Array(section.points.enumerated()), id: \.offset) { _, point in
                    VStack(alignment: .leading, spacing: Space.s1) {
                        line(point.text, at: point.at, secondary: false)
                        ForEach(Array(point.children.enumerated()), id: \.offset) { _, child in
                            line(child.text, at: child.at, secondary: true)
                                .padding(.leading, Space.s4)
                        }
                    }
                }
            }
            .padding(.leading, 24 + Space.s3)
        }
    }

    /// A bullet line: dot, text, time.
    private func line(_ text: String, at: Int?, secondary: Bool) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
            Text(verbatim: secondary ? "–" : "•").font(Typo.body).foregroundStyle(Ink.tertiary)
            Text(verbatim: text)
                .font(Typo.body).foregroundStyle(secondary ? Ink.secondary : Ink.ink)
                .lineSpacing(5).textSelection(.enabled)
            StructuredTimeLink(at: at, onJump: onJump)
        }
    }

    // MARK: Concepts and review

    private var conceptsView: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text("关键概念").font(Typo.section).padding(.bottom, Space.s3)
            ForEach(Array(notes.concepts.enumerated()), id: \.offset) { _, concept in
                Rectangle().fill(Ink.hairline).frame(height: 1)
                HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
                    Text(verbatim: concept.term)
                        .font(.system(size: 14, weight: .semibold))
                        .frame(width: 140, alignment: .leading)
                    HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                        Text(verbatim: concept.meaning).font(Typo.body).lineSpacing(4).textSelection(.enabled)
                        StructuredTimeLink(at: concept.at, onJump: onJump)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                .padding(.vertical, Space.s3)
            }
        }
    }

    private var reviewView: some View {
        VStack(alignment: .leading, spacing: Space.s3) {
            Text("待复习").font(Typo.section)
            ForEach(Array(notes.review.enumerated()), id: \.offset) { _, item in
                HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
                    Image(systemName: "circle").font(.system(size: 9)).foregroundStyle(Ink.tertiary)
                    Text(verbatim: item.text).font(Typo.body).lineSpacing(5).textSelection(.enabled)
                    StructuredTimeLink(at: item.at, onJump: onJump)
                }
            }
        }
    }
}
