-- 只允许应用账号创建恢复演练使用的新数据库，不扩大到其他业务数据库。
GRANT ALL PRIVILEGES ON `zeye\_restore\_%`.* TO 'sorting_hub'@'%';

-- 旧授权遗漏第一个下划线的转义，仅在该旧模式实际存在时撤销，支持重复部署。
SET @legacy_restore_grant_exists = (SELECT COUNT(*) FROM mysql.db WHERE User = 'sorting_hub' AND Host = '%' AND HEX(Db) = '7A6579655F726573746F72655C5F25');
SET @legacy_restore_grant_cleanup = IF(@legacy_restore_grant_exists > 0,
    CONCAT('REVOKE ALL PRIVILEGES ON ', CHAR(96), 'zeye_restore', CHAR(92), '_%', CHAR(96), '.* FROM ''sorting_hub''@''%'''),
    'DO 0');
PREPARE legacy_restore_grant_statement FROM @legacy_restore_grant_cleanup;
EXECUTE legacy_restore_grant_statement;
DEALLOCATE PREPARE legacy_restore_grant_statement;
