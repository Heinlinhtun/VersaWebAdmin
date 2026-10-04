const formatLocalDate = date => {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
};

export const currentBusinessDateInput = (now = new Date()) => {
  try {
    const settings = JSON.parse(localStorage.getItem('ShopSettings') || '{}');
    const value = settings.BusinessDayCutoffTime ?? settings.businessDayCutoffTime ?? '00:00';
    const [hours = 0, minutes = 0] = String(value).split(':').map(Number);
    const cutoffMinutes = (hours * 60) + minutes;
    const nowMinutes = (now.getHours() * 60) + now.getMinutes();
    if (cutoffMinutes > 0 && nowMinutes >= cutoffMinutes) {
      const nextDay = new Date(now);
      nextDay.setDate(nextDay.getDate() + 1);
      return formatLocalDate(nextDay);
    }
  } catch { /* A missing or malformed cache safely uses the calendar date. */ }
  return formatLocalDate(now);
};
