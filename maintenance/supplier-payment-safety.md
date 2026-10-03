# Supplier debt payment safety

## Deploy

- Deploy the API and both frontend supplier screens together. Older clients without
  a payment request ID are rejected by the new API; users must refresh them.
- Each tenant needs `SupplierDebtPaymentOperations`. The API creates it with
  `CREATE TABLE IF NOT EXISTS` before starting any payment transaction. The
  application account needs CREATE permission for this first-use schema setup.
  Alternatively, run `supplier-payment-operations.sql` against each tenant before
  deployment. Do not run schema DDL inside an accounting transaction.
- Do not substitute this feature for the reviewed repair of existing duplicates.
  Legacy and purchase-created payments have no allocation journal and are blocked
  from automatic reversal. The September 28 incident has its separate dry-run
  script, `repair-nttmachinery-supplier1-20260928.sql`.
- No production database changes or deployment were performed as part of this fix.

## Payment behavior

The browser persists an unresolved payment's exact payload and UUID, scoped to the
tenant, user and supplier. Closing/reopening the modal restores it. Network and
server failures retain the request; an explicit validation rejection clears it.
Changing an unresolved payment requires first resolving the original request.

The server locks the supplier, checks the UUID before checking available debt,
validates a positive amount with at most two decimals, locks purchase orders in
FIFO order, and commits the payment, allocations, debt and journal together.
The journal has a database-unique request ID and payment ID. Reusing an ID with
different details fails. Retrying a reversed payment reports its reversal and
does not create another payment. `PU` references are reserved for purchase-owned
payments because purchase editing can delete and recreate those rows.

## Admin reversal behavior

Both supplier history screens use the shared modal. Only an Admin sees reversal
actions, and the API independently enforces the Admin role. A reason is required.
Eligibility shown by the modal is preliminary; the API rechecks under locks.

For safety, only the latest supplier payment with a journal can be reversed,
and only while the supplier debt, purchase balances/statuses/dates, and returns
match the recorded post-payment state. Later activity requires reconciliation.
This intentionally does not offer a chain of automatic undos: even a later
reversal counts as subsequent activity. Opening-balance portions without invoice
allocations restore supplier debt only.

Reversal preserves the original, restores its exact invoice paid amounts and
statuses, restores supplier debt, and inserts a linked negative payment on the
reversal date. The journal records the admin's user ID, UTC audit time and reason.
The history identifies both rows. Signed payment sums net the original and
reversal; do not exclude the original while also counting its negative reversal.
An earlier period can still show its original payment, with the correction in
the reversal period. Reversal does not initiate a bank refund or supplier transfer.

## Verification

Run `npm test` and `npm run build` in `Versa.UI`.

Run `dotnet run --project tests/SupplierPayments.Tests/SupplierPayments.Tests.csproj`.
Pure validation and authorization checks always run. For database checks, set
`VERSA_PAYMENT_TEST_CONNECTION` to an isolated localhost MySQL/MariaDB instance,
without a Database field. The harness creates a uniquely named test database and
drops only that database afterwards. It never reads application connection settings.

The database suite exercises FIFO allocations, retries, conflicting payloads,
simultaneous requests, overpayment, repeated/concurrent reversals, historical
payment blocking, later payments/purchases/returns, history SQL, and transaction
rollback after injected journal/reversal failures. Local integration verification
uses MariaDB 10.4; the production dump is MySQL 8.4, so also run this suite against
the deployment's MySQL version before release.
