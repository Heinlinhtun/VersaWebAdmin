-- Run only against the customer's pos_db_nttcopy database after a backup.
-- Guarded repair for the supplied 2026-09-15 dump; rerunning changes nothing.
-- If newer activity changed this balance, review the ledger before proceeding.
START TRANSACTION;

SELECT CustomerId, CurrentBalance FROM Customers WHERE CustomerId = 106 FOR UPDATE;

UPDATE Customers c
SET c.CurrentBalance = 734600.00
WHERE c.CustomerId = 106
  AND c.CurrentBalance = 1703200.00
  AND EXISTS (SELECT 1 FROM Sales s WHERE s.SaleId = 845 AND s.CustomerId = c.CustomerId
              AND s.TotalAmount = 0 AND s.DueAmount = 0 AND s.PaidAmount = 200000)
  AND EXISTS (SELECT 1 FROM Sales s WHERE s.SaleId = 846 AND s.CustomerId = c.CustomerId
              AND s.TotalAmount = 934600 AND s.DueAmount = 734600 AND s.PaidAmount = 200000)
  AND (SELECT SUM(r.RefundAmount) FROM SaleReturns r WHERE r.SaleId = 845) = 1168600
  AND NOT EXISTS (SELECT 1 FROM SaleItems i WHERE i.SaleId = 845)
  AND (SELECT COUNT(*) FROM Sales s WHERE s.CustomerId = c.CustomerId) = 2;

SELECT ROW_COUNT() AS RepairedCustomers;
SELECT CustomerId, CurrentBalance FROM Customers WHERE CustomerId = 106;
COMMIT;
