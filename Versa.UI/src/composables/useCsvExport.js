import { useToast } from './useToast';
import { useI18n } from './useI18n';

export function useCsvExport() {
  const { addToast } = useToast();
  const { t } = useI18n();

  const exportToCsv = (filename, headers, dataRows) => {
    try {
      if (!dataRows || !dataRows.length) {
        addToast(t('common.noDataToExport') || 'No data available to export', 'error');
        return;
      }

      const escapeCsvCell = (value) => {
        let stringCell = String(value === null || value === undefined ? '' : value);
        // User-entered names/notes must stay text when opened in a spreadsheet.
        // Keep actual numeric values (including negative balances) numeric.
        if (typeof value === 'string' && /^[\s]*[=+@-]/.test(stringCell)) {
          stringCell = "'" + stringCell;
        }
        // RFC4180-ish: quote if the cell contains comma, quote, or line breaks.
        if (stringCell.includes(',') || stringCell.includes('"') || stringCell.includes('\n') || stringCell.includes('\r')) {
          return `"${stringCell.replace(/"/g, '""')}"`;
        }
        return stringCell;
      };

      // Format headers and rows
      const csvContent = [
        headers.map(escapeCsvCell).join(','),
        ...dataRows.map(row => row.map(escapeCsvCell).join(','))
      ].join('\n');

      // Add BOM and create Blob
      const csvData = "\uFEFF" + csvContent;
      const blob = new Blob([csvData], { type: 'text/csv;charset=utf-8' });
      
      // Browser-side download trigger
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = filename.endsWith('.csv') ? filename : `${filename}.csv`;
      
      // Standard robust hidden link behavior
      link.style.position = 'fixed';
      link.style.left = '-9999px';
      link.style.top = '-9999px';
      
      document.body.appendChild(link);
      link.click();
      
      // Cleanup
      setTimeout(() => {
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);
      }, 1000);

      addToast(t('common.exportSuccess') || 'Report exported successfully', 'success');
    } catch (err) {
      console.error("CSV Export Utility Error:", err);
      addToast(t('common.exportFailed') || 'CSV Export Failed', 'error');
    }
  };

  return { exportToCsv };
}
