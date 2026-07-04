const fs = require('fs');

const path = 'c:\\Users\\Hainn\\source\\repos\\VersaWebAdmin\\Versa.UI\\src\\views\\Products.vue';
let content = fs.readFileSync(path, 'utf8');

const replacements = [
  ['>Bulk Price Adjustment<', '>{{ t("products.bulkPriceAdjustment") }}<'],
  ['>\n              New Adjustment\n            </', '>\n              {{ t("products.newAdjustment") }}\n            </'],
  ['>\n              Adjustment History\n            </', '>\n              {{ t("products.adjustmentHistory") }}\n            </'],
  ['>\n                Step 1: Filter Products</h4>', '>\n                {{ t("products.step1FilterProducts") }}</h4>'],
  ['>Category</label>', '>{{ t("products.category") }}</label>'],
  ['>All Categories</option>', '>{{ t("products.allCategories") }}</option>'],
  ['>Search Name /\n                    SKU</label>', '>{{ t("products.searchNameSku") }}</label>'],
  ['>\n                Step\n                2: Adjustment Rules</h4>', '>\n                {{ t("products.step2AdjustmentRules") }}</h4>'],
  ['>Target Price</label>', '>{{ t("products.targetPrice") }}</label>'],
  ['>Cost Price (Buy)</option>', '>{{ t("products.costPriceBuy") }}</option>'],
  ['>Standard Retail Price</option>', '>{{ t("products.standardRetailPrice") }}</option>'],
  ['>Operation</label>', '>{{ t("products.operation") }}</label>'],
  ['>Add (+)</option>', '>{{ t("products.addPlus") }}</option>'],
  ['>Subtract (-)</option>', '>{{ t("products.subtractMinus") }}</option>'],
  ['>Value Type</label>', '>{{ t("products.valueType") }}</label>'],
  ['>Percentage (%)</option>', '>{{ t("products.percentage") }}</option>'],
  ['>Fixed Amount (Ks)</option>', '>{{ t("products.fixedAmountKs") }}</option>'],
  ['>Value</label>', '>{{ t("products.value") }}</label>'],
  ['>\n                  Step 3: Preview Changes</h4>', '>\n                  {{ t("products.step3PreviewChanges") }}</h4>'],
  ['Affected: {{ bulkAdjustPreviewData.length }}', '{{ t("products.affected") }} {{ bulkAdjustPreviewData.length }}'],
  ['>SKU</th>', '>{{ t("products.sku") }}</th>'],
  ['>Product Name</th>', '>{{ t("products.productName") }}</th>'],
  ['>Current {{ bulkAdjustConfig.target === \'CostPrice\' ? \'Cost\' : \'Retail\'\n                        }}\n                        Price</th>', '>{{ bulkAdjustConfig.target === \'CostPrice\' ? t("products.currentCostPrice") : t("products.currentRetailPrice") }}</th>'],
  ['>New Price Preview</th>', '>{{ t("products.newPricePreview") }}</th>'],
  ['>No matching products found</td>', '>{{ t("products.noMatchingProductsFound") }}</td>'],
  ['>Cancel</button>', '>{{ t("products.cancelBtn") }}</button>'],
  ['Confirm & Apply to {{ bulkAdjustPreviewData.length }} Items', '{{ t("products.confirmAndApplyTo") }} {{ bulkAdjustPreviewData.length }} {{ t("products.itemsLabel") }}'],
  ['>Rollback Notice</h5>', '>{{ t("products.rollbackNotice") }}</h5>'],
  ['>Rolling back will restore products to their exact prices\n                  at the time of adjustment. This may override manual changes made after that adjustment.</p>', '>{{ t("products.rollbackNoticeDesc") }}</p>'],
  ['>No Adjustment History</div>', '>{{ t("products.noAdjustmentHistory") }}</div>'],
  ['{{ hist.target === \'SalePrice\' ? \'Retail\' : \'Cost\' }}</span>', '{{ hist.target === \'SalePrice\' ? t("products.retailTarget") : t("products.costTarget") }}</span>'],
  ['Products Affected</span>', '{{ t("products.productsAffected") }}</span>'],
  ['Rollback\n                </button>', '{{ t("products.rollback") }}\n                </button>'],

  // Product Modal
  ['>{{ isEdit ? \'Update Product Information\' : \'Register New Item\' }}</h3>', '>{{ isEdit ? t("products.updateProductInformation") : t("products.registerNewItem") }}</h3>'],
  ['>Product Name\n                  *</label>', '>{{ t("products.productNameStar") }}</label>'],
  ['placeholder="Enter full name"', ':placeholder="t(\'products.enterFullName\')"'],
  ['>SKU / Barcode\n                  Identifier</label>', '>{{ t("products.skuBarcodeIdentifier") }}</label>'],
  ['>Product\n                  Category</label>', '>{{ t("products.productCategory") }}</label>'],
  ['placeholder="Type or select category"', ':placeholder="t(\'products.typeOrSelectCategory\')"'],
  ['>Primary Base\n                  Unit</label>', '>{{ t("products.primaryBaseUnit") }}</label>'],
  ['placeholder="Type or select unit"', ':placeholder="t(\'products.typeOrSelectUnit\')"'],
  ['>Base Cost\n                (Buy)</label>', '>{{ t("products.baseCostBuy") }}</label>'],
  ['>Min. Stock\n                Alert Level</label>', '>{{ t("products.minStockAlertLevel") }}</label>'],
  ['>Scaling Units (Bulk Support)</h4>', '>{{ t("products.scalingUnitsBulkSupport") }}</h4>'],
  ['>+\n                Add Scaling Unit</button>', '>{{ t("products.addScalingUnit") }}</button>'],
  ['>Standard base unit only.</div>', '>{{ t("products.standardBaseUnitOnly") }}</div>'],
  ['>Scaling From</label>', '>{{ t("products.scalingFrom") }}</label>'],
  ['>Quantity\n                  Multiplier</label>', '>{{ t("products.quantityMultiplier") }}</label>'],
  ['>Specific Sale\n                  Price</label>', '>{{ t("products.specificSalePrice") }}</label>'],
  ['>Wholesale & VIP Discount Tiers</h4>', '>{{ t("products.wholesaleVIPDiscountTiers") }}</h4>'],
  ['>+\n                Add Pricing Tier</button>', '>{{ t("products.addPricingTier") }}</button>'],
  ['>Standard single-tier pricing.</div>', '>{{ t("products.standardSingleTierPricing") }}</div>'],
  ['>Target Unit</label>', '>{{ t("products.targetUnit") }}</label>'],
  ['(Main)</option>', '{{ t("products.mainUnit") }}</option>'],
  ['>Tier Name</label>', '>{{ t("products.tierName") }}</label>'],
  ['>Tier Price</label>', '>{{ t("products.tierPrice") }}</label>'],
  ['>Min. Qty</label>', '>{{ t("products.minQty") }}</label>'],
  ['>Discard</button>', '>{{ t("products.discard") }}</button>'],
  ['>Save\n              Product Registry</button>', '>{{ t("products.saveProductRegistry") }}</button>'],

  // Service Modal
  ['>Service Label\n              Name</label>', '>{{ t("products.serviceLabelName") }}</label>'],
  ['placeholder="e.g. Delivery Charge"', ':placeholder="t(\'products.egDeliveryCharge\')"'],
  ['>Service Fee (Ks)</label>', '>{{ t("products.serviceFeeKs") }}</label>'],
  ['>Brief Description</label>', '>{{ t("products.briefDescription") }}</label>'],
  ['placeholder="Details about this service..."', ':placeholder="t(\'products.detailsAboutThisService\')"'],
  ['>Confirm\n            Service Account</button>', '>{{ t("products.confirmServiceAccount") }}</button>'],

  // Bundle Modal
  ['>{{ isEdit ? \'Modify Bundle\' : \'Create Promotion Bundle\'\n          }}', '>{{ isEdit ? t("products.modifyBundle") : t("products.createPromotionBundle") }}\n          '],
  ['>Bundle Label</label>', '>{{ t("products.bundleLabel") }}</label>'],
  ['placeholder="e.g. Combo Set A"', ':placeholder="t(\'products.egComboSetA\')"'],
  ['>Bundle Price\n                (Package)</label>', '>{{ t("products.bundlePricePackage") }}</label>'],
  ['>Add Products to\n              Package</label>', '>{{ t("products.addProductsToPackage") }}</label>'],
  ['>-- Select Product --</option>', '>{{ t("products.selectProduct") }}</option>'],
  ['placeholder="Qty"', ':placeholder="t(\'products.qtyHeader\')"'],
  ['>Included Items List:</h4>', '>{{ t("products.includedItemsList") }}</h4>'],
  ['>Empty\n              List</div>', '>{{ t("products.emptyList") }}</div>'],
  ['Units</div>', '{{ t("products.unitsLabel") }}</div>'],
  ['>Save\n            Bundle Account</button>', '>{{ t("products.saveBundleAccount") }}</button>'],
  ['>Bundle Components</h3>', '>{{ t("products.bundleComponents") }}</h3>'],
  ['>Quantity</th>', '>{{ t("products.quantityLabel") }}</th>'],
  ['>Close\n                    View</button>', '>{{ t("products.closeView") }}</button>']

];

let c = 0;
let fail = 0;
replacements.forEach(([search, replace]) => {
  if (content.includes(search)) {
    content = content.replace(search, replace);
    c++;
  } else {
    console.warn(`[FAIL] Not found:`, search.substring(0, 50));
    fail++;
  }
});

fs.writeFileSync(path, content, 'utf8');
console.log(`Success: ${c}, Failed: ${fail}`);
