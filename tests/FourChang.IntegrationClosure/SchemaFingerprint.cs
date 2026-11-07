static class SchemaFingerprint { public const string SchemaQuery = """
SELECT 'CHECK' AS kind,OBJECT_NAME(parent_object_id) AS tableName,name,definition,
    CONVERT(int,is_disabled) AS disabled,CONVERT(int,is_not_trusted) AS extra FROM sys.check_constraints
UNION ALL
SELECT 'TRIGGER',OBJECT_NAME(parent_id),name,OBJECT_DEFINITION(object_id),CONVERT(int,is_disabled),0
    FROM sys.triggers WHERE parent_class=1
UNION ALL
SELECT 'INDEX',OBJECT_NAME(i.object_id),i.name,
    CONCAT(i.filter_definition,'|',(SELECT STRING_AGG(CONVERT(nvarchar(max),CONCAT(c.name,':',ic.key_ordinal,':',ic.is_descending_key)),',') WITHIN GROUP(ORDER BY ic.key_ordinal)
        FROM sys.index_columns ic JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
        WHERE ic.object_id=i.object_id AND ic.index_id=i.index_id AND ic.key_ordinal>0)),
    CONVERT(int,i.is_disabled),CONVERT(int,i.is_unique)
    FROM sys.indexes i WHERE i.is_unique=1 AND OBJECT_SCHEMA_NAME(i.object_id)='dbo'
UNION ALL
SELECT 'FK',OBJECT_NAME(f.parent_object_id),f.name,
    CONCAT(OBJECT_SCHEMA_NAME(f.referenced_object_id),'.',OBJECT_NAME(f.referenced_object_id),'|',f.delete_referential_action,':',f.update_referential_action,'|',
        (SELECT STRING_AGG(CONVERT(nvarchar(max),CONCAT(COL_NAME(k.parent_object_id,k.parent_column_id),'=',COL_NAME(k.referenced_object_id,k.referenced_column_id))),',') WITHIN GROUP(ORDER BY k.constraint_column_id)
         FROM sys.foreign_key_columns k WHERE k.constraint_object_id=f.object_id)),
    CONVERT(int,f.is_disabled),CONVERT(int,f.is_not_trusted) FROM sys.foreign_keys f
UNION ALL
SELECT 'COLUMN',OBJECT_NAME(c.object_id),c.name,
    CONCAT(TYPE_NAME(c.user_type_id),':',c.max_length,':',c.precision,':',c.scale,':',c.is_nullable,':',c.is_identity,':',c.collation_name,':',OBJECT_DEFINITION(c.default_object_id)),0,0
    FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(c.object_id)='dbo'
ORDER BY kind,tableName,name
""";
}
