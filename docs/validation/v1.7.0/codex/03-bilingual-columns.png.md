系统设计 / System Design

缓存策略

使用 Redis 缓存热点数据。

命中率为 92.7%，TTL 为 300 秒。

缓存失效后读取 PostgreSQL。

监控 P99 延迟与错误率。

Deployment Notes

Use a rolling deployment.

Keep 2 replicas available.

Rollback if errors exceed 0.5%.

CPU budget: 750 millicores.

Release owner: Platform Team.