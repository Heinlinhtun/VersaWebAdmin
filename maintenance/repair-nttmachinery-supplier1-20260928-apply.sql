-- Incident: supplier 1, payment 92 retained, duplicate payments 93-104 removed.
-- Evidence: 2026-09-28 backup; 12 * 601456 = 7217472 excess deduction.
-- Confirm that only ONE real debt payment of 601456 Ks occurred.
-- Take a fresh backup and pause this tenant's writes before running.
-- Run this entire file in a MySQL client supporting DELIMITER (e.g. Workbench).
-- APPLY VERSION: commits the repair after all safety checks pass.
-- Archives the 12 removed rows; aborts and rolls back if the expected records differ.
-- Requires CREATE ROUTINE, CREATE TABLE and ordinary read/write privileges.

USE pos_db_nttmachinery;
SET @repair_old_time_zone = @@session.time_zone;
SET SESSION time_zone = '+00:00';

CREATE TABLE IF NOT EXISTS SupplierPayments_Archive_20260928_S1
LIKE SupplierPayments;

DELIMITER $$
DROP PROCEDURE IF EXISTS repair_supplier1_duplicates_20260928$$
CREATE PROCEDURE repair_supplier1_duplicates_20260928(IN apply_changes BOOLEAN)
BEGIN
    DECLARE old_debt DECIMAL(18,2) DEFAULT NULL;
    DECLARE matching_count INT DEFAULT 0;
    DECLARE affected_count INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    SELECT CurrentDebt INTO old_debt
    FROM Suppliers
    WHERE SupplierId = 1 AND Phone = '09784747800'
    FOR UPDATE;

    IF old_debt IS NULL OR old_debt <> -7217472.00 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Supplier/balance changed or repair already applied. Stop and reconcile.';
    END IF;

    SELECT PaymentId, SupplierId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate
    FROM SupplierPayments WHERE SupplierId = 1 FOR UPDATE;

    SELECT POId, TotalAmount, PaidAmount, Status
    FROM PurchaseOrders WHERE SupplierId = 1 FOR UPDATE;

    SELECT COUNT(*) INTO matching_count
    FROM SupplierPayments
    WHERE SupplierId = 1 AND PaymentId BETWEEN 92 AND 104
      AND Amount = 601456.00 AND PaymentMethod = 'Cash'
      AND ReferenceNo = '' AND Note = 'Debt payment'
      AND PaymentDate = '2026-09-28 00:00:00';

    IF matching_count <> 13 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Expected all 13 original payment rows. Stop and reconcile.';
    END IF;

    SELECT COUNT(*) INTO matching_count
    FROM PurchaseOrders
    WHERE POId = 698 AND SupplierId = 1
      AND TotalAmount = 676638.00 AND PaidAmount = 676638.00 AND Status = 'Paid';
    IF matching_count <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Purchase 698 differs from the investigated backup.';
    END IF;

    IF EXISTS (SELECT 1 FROM PurchaseOrders WHERE SupplierId = 1
               AND (TotalAmount IS NULL OR PaidAmount IS NULL OR TotalAmount <> PaidAmount))
       OR EXISTS (SELECT 1 FROM PurchaseReturns r JOIN PurchaseOrders p ON p.POId = r.POId
                  WHERE p.SupplierId = 1) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Purchase balances or returns need separate reconciliation.';
    END IF;

    INSERT INTO SupplierPayments_Archive_20260928_S1
        (PaymentId, SupplierId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate)
    SELECT PaymentId, SupplierId, Amount, PaymentMethod, ReferenceNo, Note, PaymentDate
    FROM SupplierPayments
    WHERE SupplierId = 1 AND PaymentId BETWEEN 93 AND 104;
    SET affected_count = ROW_COUNT();
    IF affected_count <> 12 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Expected exactly 12 archived payments.';
    END IF;

    DELETE FROM SupplierPayments
    WHERE SupplierId = 1 AND PaymentId BETWEEN 93 AND 104;
    SET affected_count = ROW_COUNT();
    IF affected_count <> 12 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Expected exactly 12 removed payments.';
    END IF;

    UPDATE Suppliers SET CurrentDebt = CurrentDebt + 7217472.00
    WHERE SupplierId = 1 AND CurrentDebt = -7217472.00;
    SET affected_count = ROW_COUNT();
    IF affected_count <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Supplier update failed.';
    END IF;

    SELECT SupplierId, SupplierName, CurrentDebt FROM Suppliers WHERE SupplierId = 1;
    SELECT 92 AS RetainedPaymentId, 12 AS RemovedDuplicates, 7217472.00 AS DebtRestored;

    IF apply_changes THEN
        COMMIT;
        SELECT 'COMMITTED: payment 92 retained; 93-104 archived and removed; debt is zero.' AS Result;
    ELSE
        ROLLBACK;
        SELECT 'DRY RUN: data changes rolled back. Change CALL argument to TRUE to apply.' AS Result;
    END IF;
END$$
DELIMITER ;

CALL repair_supplier1_duplicates_20260928(TRUE);

DROP PROCEDURE repair_supplier1_duplicates_20260928;
SET SESSION time_zone = @repair_old_time_zone;
