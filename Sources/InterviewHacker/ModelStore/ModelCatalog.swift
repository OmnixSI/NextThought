import Foundation

/// The models the app can download, with everything the app needs to know
/// about them: where they come from, which revision the hashes belong to, how
/// big they are and what they are good for.
///
/// The weights never ship with the app. They are downloaded on request into
/// the data directory, checked against the hashes below and can be removed at
/// any time by deleting the model folder.
enum ModelCatalog {
    /// Baidu Unlimited-OCR (3B MoE, ~0.5B active) in the MLX port of Fluid
    /// Inference: the page encoder and the multi-page decoder are Core ML
    /// models, so they run on the Neural Engine and the GPU without any
    /// Python and without bundling a second runtime.
    static let unlimitedOCR = ManagedModelDescriptor(
        id: "unlimited-ocr-mlx-coreml",
        name: "Unlimited-OCR 文档识别",
        kind: .ocr,
        repository: "FluidInference/unlimited-ocr-mlx-coreml",
        revision: "4568571eacd864e0a253c609b3c9d0e00450cebc",
        files: [
            ManagedModelFile(
                path: "config.json", sha256: "210d30d4698482e1672cbbb83846d88b5ca2b67333a3fbbbefa056663080945b",
                size: 704, weight: 0.001),
            ManagedModelFile(
                path: "tokenizer.json", sha256: "a02f8fd5228c90256bb4f6554c34a579d48f909e5beb232dc4afad870b55a8b4",
                size: 9_979_544, weight: 10),
            ManagedModelFile(
                path: "embeddings.f16", sha256: "5d1df8b73bdff79c672f53ddc000451158cd16cd04ed143a1c0054b30ca03455",
                size: 330_956_800, weight: 331),
            ManagedModelFile(
                path: "UnlimitedOcrPageEncoder.mlpackage/Manifest.json",
                sha256: "614ed2ad82f254936583de24a8001d440c39b00828f81343791f9eaa42913d1a",
                size: 617, weight: 0.001),
            ManagedModelFile(
                path: "UnlimitedOcrPageEncoder.mlpackage/Data/com.apple.CoreML/model.mlmodel",
                sha256: "7dcce3bed4dcf35e3dde7bd2510aa7cbc7b1bd3ca8eff93d859d11456005fc9d",
                size: 480_813, weight: 0.5),
            ManagedModelFile(
                path: "UnlimitedOcrPageEncoder.mlpackage/Data/com.apple.CoreML/weights/weight.bin",
                sha256: "767a77ddc86f6721dfe1e46fbb44c1e624429dfc3920682e49f68734189e40f9",
                size: 806_020_480, weight: 806),
            ManagedModelFile(
                path: "UnlimitedOcrDecoder.mlpackage/Manifest.json",
                sha256: "b0086733d3888c7cb881628e6a60d227a6f1c63f09c86db7ed21eb633e5dd3de",
                size: 617, weight: 0.001),
            ManagedModelFile(
                path: "UnlimitedOcrDecoder.mlpackage/Data/com.apple.CoreML/model.mlmodel",
                sha256: "4a339171fbfd8ca84a1de4d39de77d53538feb7e4bb9759d3fb92b3d0ee20ee2",
                size: 3_423_081, weight: 3.5),
            ManagedModelFile(
                path: "UnlimitedOcrDecoder.mlpackage/Data/com.apple.CoreML/weights/weight.bin",
                sha256: "9dca216cc86a99767f5837c2b17e132c96001ec21006c12eae45830fbebc468e",
                size: 2_942_897_664, weight: 2_943),
        ],
        summary: "百度 Unlimited-OCR 的 MLX 特化版：整页文档转 Markdown，含表格、公式与版面坐标。",
        license: "MIT（模型权重 © 百度，转换格式 © Fluid Inference）",
        homepage: "https://huggingface.co/FluidInference/unlimited-ocr-mlx-coreml",
        note: "下载约 3.8 GB，需要 macOS 15 及以上。默认用于扫描件与图片的整页识别，失败时自动回退到系统 OCR。",
        minimumOS: "macOS 15")

    /// Qwen3-Embedding-0.6B in the Core ML conversion of neuradex: the model
    /// with the best Chinese/English retrieval quality per size that is small
    /// enough to sit on a laptop, running entirely on the Neural Engine.
    static let qwen3Embedding = ManagedModelDescriptor(
        id: "qwen3-embedding-0-6b-coreml",
        name: "Qwen3-Embedding 向量检索",
        kind: .embedding,
        repository: "neuradex/Qwen3-Embedding-0.6B-CoreML-ANE",
        revision: "97d6231731046eb542d7a8aaff8628d139efdb6e",
        files: [
            ManagedModelFile(
                path: "packages/b1_s512.mlpackage/Manifest.json",
                sha256: "988f2307707e0c07ff3cb2502afa48b1084dacbbd9c638b1216cfdb270ee2192",
                size: 617, weight: 0.001),
            ManagedModelFile(
                path: "packages/b1_s512.mlpackage/Data/com.apple.CoreML/model.mlmodel",
                sha256: "8abb0859ab87994454bdccbedde8dd861d13cee0e940666901e3651539588f73",
                size: 616_252, weight: 0.6),
            ManagedModelFile(
                path: "packages/b1_s512.mlpackage/Data/com.apple.CoreML/weights/weight.bin",
                sha256: "410986b0b9c2659dd50e297b640c9aa29a17b2762b24f1ec0a89a3507d0e7c51",
                size: 1_192_372_032, weight: 1_192),
            ManagedModelFile(
                path: "tokenizer/tokenizer.json",
                sha256: "def76fb086971c7867b829c23a26261e38d9d74e02139253b38aeb9df8b4b50a",
                size: 11_423_705, weight: 11.5),
            ManagedModelFile(
                path: "tokenizer/tokenizer_config.json",
                sha256: "443bfa629eb16387a12edbf92a76f6a6f10b2af3b53d87ba1550adfcf45f7fa0",
                size: 5_404, weight: 0.01),
            ManagedModelFile(
                path: "tokenizer/special_tokens_map.json",
                sha256: "76862e765266b85aa9459767e33cbaf13970f327a0e88d1c65846c2ddd3a1ecd",
                size: 613, weight: 0.001),
            ManagedModelFile(
                path: "tokenizer/added_tokens.json",
                sha256: "c0284b582e14987fbd3d5a2cb2bd139084371ed9acbae488829a1c900833c680",
                size: 707, weight: 0.001),
            ManagedModelFile(
                path: "tokenizer/merges.txt",
                sha256: "8831e4f1a044471340f7c0a83d7bd71306a5b867e95fd870f74d0c5308a904d5",
                size: 1_671_853, weight: 1.7),
            ManagedModelFile(
                path: "tokenizer/vocab.json",
                sha256: "ca10d7e9fb3ed18575dd1e277a2579c16d108e32f27439684afa0e10b1440910",
                size: 2_776_833, weight: 2.8),
            ManagedModelFile(
                path: "tokenizer/chat_template.jinja",
                sha256: "87a2728cb8dc9fe424d624542f6060ec05a1d285ebbec578bb078900e33396b5",
                size: 4_116, weight: 0.01),
        ],
        summary: "阿里 Qwen3-Embedding-0.6B：中文/英文检索效果最好的小模型之一，1024 维向量。",
        license: "Apache-2.0（模型 © 阿里巴巴）",
        homepage: "https://huggingface.co/neuradex/Qwen3-Embedding-0.6B-CoreML-ANE",
        note: "下载约 1.1 GB。装好后资料检索会同时用关键词和语义两路召回，再融合排序。",
        minimumOS: "macOS 14")

    /// Every model the app can manage, in the order the settings list shows them.
    static let all: [ManagedModelDescriptor] = [unlimitedOCR, qwen3Embedding]

    static func descriptor(for id: String) -> ManagedModelDescriptor? {
        all.first { $0.id == id }
    }
}
