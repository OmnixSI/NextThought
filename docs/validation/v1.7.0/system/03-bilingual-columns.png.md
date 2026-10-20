系统设计 / System Design
缓存策略
使用 Redis 缓存热点数据。
命中率为92.7%，TTL为300秒。
缓存失效后读取 PostgreSQL。
监控 P99延迟与错误率。
Deployment Notes
Use arolling deployment.
Keep 2replicas available.
Rollback if errors exceed 0.5%.
CPU budget: 750 millicores.
Release owner: Platform Team.