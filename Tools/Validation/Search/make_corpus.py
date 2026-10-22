"""Authored routing cases; expected labels are frozen before running a model."""
import json,pathlib
fresh=[
'今天悉尼天气怎么样？','现在美元兑人民币是多少？','苹果现任 CEO 是谁？','最近 OpenAI 发布了什么模型？','这周英伟达股价变化如何？',
'Claude 支持多长的上下文？','Gemini 支持哪些输入模态？','ChatGPT 现在的订阅费用是多少？','Qwen 最新开源模型有哪些？','PyTorch 当前支持哪个 CUDA 版本？',
'这个方法和 SOTA 相比还有优势吗？','近场波束选择的研究进展如何？','澳大利亚学生签证现在的工作时长限制是多少？','悉尼到墨尔本明天有哪些航班？','Swift 最新正式版是多少？',
'这家公司最近一季营收是多少？','今年诺贝尔物理学奖是谁获得？','最近有哪些 AI 论文在讨论这个问题？','当前澳大利亚央行现金利率是多少？','这个平台现在还提供免费 API 额度吗？',
'这个基金今年的申请截止日期是什么时候？','澳大利亚最低工资现在是多少？','今年 IEEE ICC 在哪里举办？','iPhone 当前在售的型号有哪些？','现在 Llama 的商用许可有什么限制？',
'What is the current price of Bitcoin?','Who is the CEO of Microsoft?','How much does the Claude API cost?','Which Python version is currently supported by PyTorch?',
'What is the latest Android API level?','Does Gemini support live video input?','What are the current US visa processing times?','Who won the most recent Formula 1 race?',
'What are the latest papers on conformal prediction for wireless systems?','Which model leads the current coding benchmark?','Is this conference submission deadline still open?',
'How large is the GPT context window?','What is the current tax-free threshold in Australia?','Which CUDA release works with the newest PyTorch?',
'What are the current prices of M4 MacBook Pro models?','Has this package fixed the vulnerability yet?','Who currently chairs the FCC?','What are the recent changes to the student visa policy?',
'Can DeepSeek process images now?','How much free storage does this cloud provider offer?','今天的澳元汇率是多少？','最近可商用的语音识别模型有哪些？','今年 UTS 博士奖学金金额是多少？','这篇方法与当前主流大模型方案比如何？','我该如何查看现在的最新软件发布记录？']
stable=[
'什么是牛顿第二定律？','近场与远场的物理区别是什么？','为什么阵列孔径会影响瑞利距离？','解释一下共形预测的覆盖率。','矩阵特征值是什么？',
'二分查找的时间复杂度是多少？','Redis 和 Memcached 有何区别？','Python 列表和元组有什么区别？','数据库事务的 ACID 是什么？','TCP 三次握手为什么需要三次？',
'你最近在做什么项目？','介绍一下你目前负责的工作。','你们实验用了多少个样本？','这篇论文里的实验结果是什么？','你刚才提到的个人经历是什么？',
'请把上一句话翻译成英文。','帮我把上面的解释缩短。','什么是梯度下降？','为什么过拟合会发生？','解释条件概率与联合概率。',
'如何实现线程安全的队列？','什么是分布式系统的一致性？','给出勾股定理的证明思路。','什么是信号的信噪比？','你在这个项目里承担了什么职责？',
'What is a singular value decomposition?','Explain the difference between near-field and far-field propagation.','How does binary search work?','Why does regularization reduce overfitting?',
'What is the principle of conformal prediction?','Explain the TCP handshake.','What is a database transaction?','How do you implement an LRU cache?',
'Can you walk me through your current project?','What did you personally contribute to this paper?','How many samples did your experiment use?',
'Translate the previous answer into Chinese.','Summarize the previous explanation in two sentences.','What is the chain rule in calculus?',
'Why is the Fourier transform useful?','How does backpropagation compute gradients?','What is the difference between precision and recall?',
'Explain covariance and correlation.','What is Bayes theorem?','How does a hash table handle collisions?','为什么光速在真空中不变？','这个公式如何从上一行推导出来？','我们自己的消融实验做了哪些？','你为什么选择读博士？','解释近场聚焦与远场定向的差异。']
assert len(fresh)==len(stable)==50
samples=[]
for label,questions in [(True,fresh),(False,stable)]:
 for i,q in enumerate(questions):samples.append({'question':q,'needsSearch':label,'split':'validation' if i<25 else 'heldout'})
p=pathlib.Path(__file__).with_name('corpus.json');p.write_text(json.dumps(samples,ensure_ascii=False,indent=2)+'\n')
