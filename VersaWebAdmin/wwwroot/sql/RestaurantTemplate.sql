-- 1. Create Database
CREATE DATABASE IF NOT EXISTS `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `{DatabaseName}`;

-- 2. Create Tables
CREATE TABLE `Categories` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(100) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `Floors` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(100) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `Settings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `ShopName` longtext NOT NULL,
    `CurrencySymbol` longtext NOT NULL,
    `Language` longtext NOT NULL,
    `LogoUrl` longtext NULL,
    `ShopAddress` longtext NULL,
    `ShopPhone` longtext NULL,
    `ConnectionString` longtext NULL,
    `KitchenPrinterName` longtext NULL,
    `FrontPrinterName` longtext NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `Staff` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `FullName` longtext NOT NULL,
    `Role` int NOT NULL, -- 0: Admin, 1: Manager, 2: Waiter, etc.
    `Phone` longtext NOT NULL,
    `Email` longtext NOT NULL,
    `PinCode` longtext NOT NULL,
    `IsActive` tinyint(1) NOT NULL DEFAULT 1,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `InventoryItems` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(200) NOT NULL,
    `Quantity` decimal(10,2) NOT NULL,
    `Unit` varchar(50) NOT NULL,
    `MinimumQuantity` decimal(10,2) NOT NULL,
    `LastUpdated` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `MenuItems` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(200) NOT NULL,
    `Description` varchar(500) NOT NULL,
    `Price` decimal(10,2) NOT NULL,
    `CategoryId` int NOT NULL,
    `IsAvailable` tinyint(1) NOT NULL,
    `ImageUrl` varchar(500) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_MenuItems_Categories_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `Categories` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `Tables` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TableNumber` int NOT NULL,
    `Capacity` int NOT NULL,
    `Status` int NOT NULL, -- 0: Available, 1: Occupied, etc.
    `FloorId` int NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Tables_Floors_FloorId` FOREIGN KEY (`FloorId`) REFERENCES `Floors` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `Orders` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `TableId` int NOT NULL,
    `TotalAmount` decimal(10,2) NOT NULL,
    `Status` int NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `UpdatedAt` datetime(6) NULL,
    `Notes` varchar(500) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Orders_Tables_TableId` FOREIGN KEY (`TableId`) REFERENCES `Tables` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `OrderItems` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `OrderId` int NOT NULL,
    `MenuItemId` int NOT NULL,
    `Quantity` int NOT NULL,
    `UnitPrice` decimal(10,2) NOT NULL,
    `Subtotal` decimal(10,2) NOT NULL,
    `SpecialInstructions` varchar(300) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_OrderItems_MenuItems_MenuItemId` FOREIGN KEY (`MenuItemId`) REFERENCES `MenuItems` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_OrderItems_Orders_OrderId` FOREIGN KEY (`OrderId`) REFERENCES `Orders` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE `Reservations` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `CustomerName` varchar(200) NOT NULL,
    `CustomerPhone` varchar(20) NOT NULL,
    `ReservationDate` datetime(6) NOT NULL,
    `NumberOfPeople` int NOT NULL,
    `Status` int NOT NULL,
    `TableId` int NULL,
    `CreatedAt` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Reservations_Tables_TableId` FOREIGN KEY (`TableId`) REFERENCES `Tables` (`Id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- 3. Initial Seed Data
INSERT INTO `Categories` (`Id`, `Name`) VALUES 
(1, 'Appetizers'), (2, 'Main Course'), (3, 'Desserts'), (4, 'Beverages'), (5, 'Salads');

INSERT INTO `Floors` (`Id`, `Name`) VALUES 
(1, 'Main Floor'), (2, 'Terrace'), (3, 'Private Room');

INSERT INTO `Settings` (`Id`, `ShopName`, `CurrencySymbol`, `Language`) VALUES 
(1, 'Restaurant System', '$', 'en-US');

-- DEFAULT ADMIN USER (PIN: 1234)
-- This will be handled during registration to use the provided username/password if needed,
-- but the script provided by the user has a fixed seed. We will override it in the controller logic.
INSERT INTO `Staff` (`FullName`, `Role`, `Phone`, `Email`, `PinCode`, `IsActive`, `CreatedAt`) VALUES 
('Admin User', 0, '09000000000', 'admin@example.com', '1234', 1, UTC_TIMESTAMP());

INSERT INTO `Tables` (`TableNumber`, `Capacity`, `Status`, `FloorId`) VALUES 
(1, 2, 0, 1), (2, 2, 0, 1), (3, 4, 0, 1), (4, 4, 0, 1), (5, 6, 0, 1),
(6, 6, 0, 2), (7, 8, 0, 2), (8, 8, 0, 2), (9, 10, 0, 3), (10, 4, 0, 3);

INSERT INTO `InventoryItems` (`Name`, `Quantity`, `Unit`, `MinimumQuantity`, `LastUpdated`) VALUES 
('Rice', 50.00, 'kg', 10.00, UTC_TIMESTAMP()),
('Chicken Breast', 20.00, 'kg', 5.00, UTC_TIMESTAMP()),
('Salmon Fillet', 15.00, 'kg', 3.00, UTC_TIMESTAMP());

INSERT INTO `MenuItems` (`Name`, `Description`, `Price`, `CategoryId`, `IsAvailable`, `ImageUrl`) VALUES 
('Spring Rolls', 'Crispy vegetable spring rolls', 8.99, 1, 1, 'https://images.unsplash.com/photo-1544025162-d76694265947'),
('Beef Steak', '8oz ribeye steak cooked to perfection', 29.99, 2, 1, 'https://images.unsplash.com/photo-1600891964092-4316c288032e'),
('Tiramisu', 'Classic Italian dessert', 9.99, 3, 1, 'https://images.unsplash.com/photo-1571877227200-a0d98ea607e9');
