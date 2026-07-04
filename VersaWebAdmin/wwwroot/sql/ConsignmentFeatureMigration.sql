/*
  Consignment feature migration

  How to run:
    1. Connect to the tenant POS database.
    2. Run this whole script.

  The script is idempotent. It checks INFORMATION_SCHEMA before changing columns
  and indexes, so it can be run more than once safely.
*/

SET @target_schema = DATABASE();

DELIMITER $$

DROP PROCEDURE IF EXISTS AddColumnIfMissing $$
CREATE PROCEDURE AddColumnIfMissing(
    IN p_schema_name VARCHAR(128),
    IN p_table_name VARCHAR(128),
    IN p_column_name VARCHAR(128),
    IN p_column_definition TEXT
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = p_schema_name
          AND TABLE_NAME = p_table_name
          AND COLUMN_NAME = p_column_name
    ) THEN
        SET @ddl = CONCAT(
            'ALTER TABLE `', REPLACE(p_schema_name, '`', '``'), '`.`', REPLACE(p_table_name, '`', '``'),
            '` ADD COLUMN `', REPLACE(p_column_name, '`', '``'), '` ', p_column_definition
        );
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END $$

DROP PROCEDURE IF EXISTS AddIndexIfMissing $$
CREATE PROCEDURE AddIndexIfMissing(
    IN p_schema_name VARCHAR(128),
    IN p_table_name VARCHAR(128),
    IN p_index_name VARCHAR(128),
    IN p_index_definition TEXT
)
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = p_schema_name
          AND TABLE_NAME = p_table_name
          AND INDEX_NAME = p_index_name
    ) THEN
        SET @ddl = CONCAT(
            'ALTER TABLE `', REPLACE(p_schema_name, '`', '``'), '`.`', REPLACE(p_table_name, '`', '``'),
            '` ADD ', p_index_definition
        );
        PREPARE stmt FROM @ddl;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;
END $$

DROP PROCEDURE IF EXISTS CreateConsignmentTablesIfMissing $$
CREATE PROCEDURE CreateConsignmentTablesIfMissing(IN p_schema_name VARCHAR(128))
BEGIN
    SET @ddl = CONCAT(
        'CREATE TABLE IF NOT EXISTS `', REPLACE(p_schema_name, '`', '``'), '`.`Consignments` (',
        '  `ConsignId` INT NOT NULL AUTO_INCREMENT,',
        '  `SupplierId` INT DEFAULT NULL,',
        '  `BranchId` INT DEFAULT NULL,',
        '  `OrderDate` DATE DEFAULT NULL,',
        '  `Status` ENUM(''Active'',''Settled'',''Returned'') DEFAULT ''Active'',',
        '  `Remark` TEXT,',
        '  `CreatedAt` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,',
        '  PRIMARY KEY (`ConsignId`),',
        '  KEY `BranchId` (`BranchId`),',
        '  KEY `idx_consign_supplier` (`SupplierId`),',
        '  KEY `idx_consign_status` (`Status`)',
        ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci'
    );
    PREPARE stmt FROM @ddl;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;

    SET @ddl = CONCAT(
        'CREATE TABLE IF NOT EXISTS `', REPLACE(p_schema_name, '`', '``'), '`.`ConsignmentItems` (',
        '  `ConsignItemId` INT NOT NULL AUTO_INCREMENT,',
        '  `ConsignId` INT DEFAULT NULL,',
        '  `ProductId` INT DEFAULT NULL,',
        '  `UnitId` INT DEFAULT NULL,',
        '  `BranchId` INT DEFAULT NULL,',
        '  `Quantity` DECIMAL(18,2) NOT NULL,',
        '  `SoldQuantity` DECIMAL(18,2) DEFAULT ''0.00'',',
        '  `SettledQuantity` DECIMAL(18,2) NOT NULL DEFAULT ''0.00'',',
        '  `ReturnedQuantity` DECIMAL(18,2) NOT NULL DEFAULT ''0.00'',',
        '  `UnitPrice` DECIMAL(18,2) NOT NULL,',
        '  `CreatedAt` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,',
        '  PRIMARY KEY (`ConsignItemId`),',
        '  KEY `ConsignId` (`ConsignId`),',
        '  KEY `ProductId` (`ProductId`),',
        '  KEY `idx_consignment_items_branch_product` (`BranchId`, `ProductId`)',
        ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci'
    );
    PREPARE stmt FROM @ddl;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;

    SET @ddl = CONCAT(
        'CREATE TABLE IF NOT EXISTS `', REPLACE(p_schema_name, '`', '``'), '`.`ConsignmentSettlements` (',
        '  `SettlementId` INT NOT NULL AUTO_INCREMENT,',
        '  `SupplierId` INT NOT NULL,',
        '  `BranchId` INT DEFAULT NULL,',
        '  `TotalAmount` DECIMAL(18,2) NOT NULL DEFAULT ''0.00'',',
        '  `PaymentMethod` VARCHAR(50) DEFAULT NULL,',
        '  `ReferenceNo` VARCHAR(100) DEFAULT NULL,',
        '  `Note` TEXT,',
        '  `SettlementDate` TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP,',
        '  PRIMARY KEY (`SettlementId`),',
        '  KEY `idx_consignment_settlement_supplier` (`SupplierId`),',
        '  KEY `idx_consignment_settlement_date` (`SettlementDate`)',
        ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci'
    );
    PREPARE stmt FROM @ddl;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;

    SET @ddl = CONCAT(
        'CREATE TABLE IF NOT EXISTS `', REPLACE(p_schema_name, '`', '``'), '`.`ConsignmentSettlementItems` (',
        '  `SettlementItemId` INT NOT NULL AUTO_INCREMENT,',
        '  `SettlementId` INT NOT NULL,',
        '  `ConsignItemId` INT NOT NULL,',
        '  `ProductId` INT NOT NULL,',
        '  `Quantity` DECIMAL(18,2) NOT NULL,',
        '  `UnitCost` DECIMAL(18,2) NOT NULL,',
        '  `Amount` DECIMAL(18,2) NOT NULL,',
        '  PRIMARY KEY (`SettlementItemId`),',
        '  KEY `idx_csi_settlement` (`SettlementId`),',
        '  KEY `idx_csi_consign_item` (`ConsignItemId`)',
        ') ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci'
    );
    PREPARE stmt FROM @ddl;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;
END $$

DELIMITER ;

CALL CreateConsignmentTablesIfMissing(@target_schema);

CALL AddColumnIfMissing(@target_schema, 'Consignments', 'CreatedAt', 'TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP');

CALL AddColumnIfMissing(@target_schema, 'ConsignmentItems', 'UnitId', 'INT NULL AFTER `ProductId`');
CALL AddColumnIfMissing(@target_schema, 'ConsignmentItems', 'BranchId', 'INT NULL AFTER `UnitId`');
CALL AddColumnIfMissing(@target_schema, 'ConsignmentItems', 'SettledQuantity', 'DECIMAL(18,2) NOT NULL DEFAULT 0.00 AFTER `SoldQuantity`');
CALL AddColumnIfMissing(@target_schema, 'ConsignmentItems', 'ReturnedQuantity', 'DECIMAL(18,2) NOT NULL DEFAULT 0.00 AFTER `SettledQuantity`');
CALL AddColumnIfMissing(@target_schema, 'ConsignmentItems', 'CreatedAt', 'TIMESTAMP NULL DEFAULT CURRENT_TIMESTAMP');

CALL AddColumnIfMissing(@target_schema, 'SaleItems', 'IsConsignment', 'TINYINT(1) NOT NULL DEFAULT 0');
CALL AddColumnIfMissing(@target_schema, 'SaleItems', 'ConsignItemId', 'INT NULL');

CALL AddIndexIfMissing(@target_schema, 'ConsignmentItems', 'idx_consignment_items_branch_product', 'INDEX `idx_consignment_items_branch_product` (`BranchId`, `ProductId`)');
CALL AddIndexIfMissing(@target_schema, 'SaleItems', 'idx_saleitems_consignment', 'INDEX `idx_saleitems_consignment` (`IsConsignment`, `ConsignItemId`)');

SET @ddl = CONCAT(
    'CREATE OR REPLACE VIEW `', REPLACE(@target_schema, '`', '``'), '`.`vw_ConsignmentInventory` AS ',
    'SELECT ',
    '  ci.ConsignItemId, ',
    '  ci.ConsignId, ',
    '  c.SupplierId, ',
    '  COALESCE(ci.BranchId, c.BranchId) AS BranchId, ',
    '  ci.ProductId, ',
    '  ci.UnitId, ',
    '  ci.Quantity, ',
    '  ci.SoldQuantity, ',
    '  ci.SettledQuantity, ',
    '  ci.ReturnedQuantity, ',
    '  (ci.Quantity - ci.SoldQuantity - ci.ReturnedQuantity) AS AvailableQuantity, ',
    '  (ci.SoldQuantity - ci.SettledQuantity) AS UnsettledQuantity, ',
    '  ci.UnitPrice, ',
    '  ((ci.SoldQuantity - ci.SettledQuantity) * ci.UnitPrice) AS UnsettledAmount, ',
    '  c.Status, ',
    '  c.OrderDate ',
    'FROM `', REPLACE(@target_schema, '`', '``'), '`.`ConsignmentItems` ci ',
    'JOIN `', REPLACE(@target_schema, '`', '``'), '`.`Consignments` c ON c.ConsignId = ci.ConsignId'
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

DROP PROCEDURE IF EXISTS CreateConsignmentTablesIfMissing;
DROP PROCEDURE IF EXISTS AddIndexIfMissing;
DROP PROCEDURE IF EXISTS AddColumnIfMissing;
