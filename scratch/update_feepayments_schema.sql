IF NOT EXISTS (SELECT * FROM sys.indexes WHERE object_id = OBJECT_ID('Tenant.FeePayments') AND name = 'IX_FeePayments_IdempotencyKey')
BEGIN
    CREATE NONCLUSTERED INDEX IX_FeePayments_IdempotencyKey ON Tenant.FeePayments(IdempotencyKey) WHERE IdempotencyKey IS NOT NULL;
END
