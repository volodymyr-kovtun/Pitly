export function formatPln(value: number): string {
  const abs = Math.abs(value);
  const fixed = abs.toFixed(2);
  const [intPart, decPart] = fixed.split('.');
  const withSpaces = intPart.replace(/\B(?=(\d{3})+(?!\d))/g, '\u00A0');
  const formatted = `${withSpaces},${decPart} PLN`;
  return value < 0 ? `-${formatted}` : formatted;
}

export function formatUsd(value: number): string {
  return `$ ${Math.abs(value).toFixed(2)}`;
}

export function formatRate(value: number): string {
  return value.toFixed(4);
}

export function formatDate(dateStr: string): string {
  const d = new Date(dateStr);
  const dd = String(d.getDate()).padStart(2, '0');
  const mm = String(d.getMonth() + 1).padStart(2, '0');
  const yyyy = d.getFullYear();
  return `${dd}.${mm}.${yyyy}`;
}

export function numColor(value: number): string {
  if (value > 0.005) return 'text-green-400';
  if (value < -0.005) return 'text-red-400';
  return 'text-blue-400';
}
