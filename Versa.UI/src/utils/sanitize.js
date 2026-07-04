/**
 * Sanitizes error messages by removing potentially sensitive information
 * such as local file paths that may leak from server-side exceptions.
 *
 * @param {string} message - The raw error message
 * @returns {string} - The sanitized error message
 */
export function sanitizeError(message) {
  if (typeof message !== 'string') return message;

  // Strip Windows-style absolute paths (e.g., C:\Users\Name\...\file.cs)
  let sanitized = message.replace(/\b[A-Za-z]:\\[^\s,;]*\\/g, '[path]\\');

  // Strip Unix-style absolute paths (e.g., /home/user/.../file.cs)
  sanitized = sanitized.replace(/\/home\/[^\s,;]*\//g, '[path]/');
  sanitized = sanitized.replace(/\/Users\/[^\s,;]*\//g, '[path]/');
  sanitized = sanitized.replace(/\/var\/[^\s,;]*\//g, '[path]/');
  sanitized = sanitized.replace(/\/opt\/[^\s,;]*\//g, '[path]/');

  // Strip line numbers like "at file.cs:line 25"
  sanitized = sanitized.replace(/(?:at |in )([A-Za-z]:\\[^\s:]+):line\s*\d+/gi, 'at [path]:line X');

  return sanitized;
}
