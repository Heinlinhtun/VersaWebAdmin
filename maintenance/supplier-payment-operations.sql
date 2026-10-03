-- Select the intended tenant database first. Run outside any transaction.
CREATE TABLE IF NOT EXISTS SupplierDebtPaymentOperations (
    RequestId CHAR(36) NOT NULL PRIMARY KEY,
    SupplierId INT NOT NULL,
    PaymentId INT NOT NULL,
    PayloadHash CHAR(64) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    DebtAfter DECIMAL(18,2) NOT NULL,
    StateAfter CHAR(64) NOT NULL,
    Allocations LONGTEXT NOT NULL,
    CreatedBy VARCHAR(100) NOT NULL,
    CreatedAt DATETIME(6) NOT NULL,
    ReversalPaymentId INT NULL,
    ReversedBy VARCHAR(100) NULL,
    ReversedAt DATETIME(6) NULL,
    ReversalReason VARCHAR(500) NULL,
    UNIQUE KEY UQ_SupplierDebtPayment (PaymentId),
    UNIQUE KEY UQ_SupplierDebtReversal (ReversalPaymentId),
    KEY IX_SupplierDebtOperations (SupplierId)
) ENGINE=InnoDB;
