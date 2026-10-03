-- Optional pre-provisioning for each tenant. The API also creates this table
-- before starting a production transaction. Requires InnoDB for atomic writes.
CREATE TABLE IF NOT EXISTS ProductionRecipes (
    ProductId INT NOT NULL PRIMARY KEY,
    UnitId INT NOT NULL,
    RecipeJson LONGTEXT NOT NULL
) ENGINE=InnoDB;
