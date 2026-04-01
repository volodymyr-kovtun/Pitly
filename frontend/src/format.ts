const plnFormatter = new Intl.NumberFormat('pl-PL', {
  style: 'currency',
  currency: 'PLN',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export function formatPln(value: number): string {
  return plnFormatter.format(value);
}

export function formatUsd(value: number): string {
  return `$ ${Math.abs(value).toFixed(2)}`;
}

export function formatRate(value: number): string {
  return value.toFixed(4);
}

const dateFormatter = new Intl.DateTimeFormat('pl-PL');

export function formatDate(dateStr: string): string {
  return dateFormatter.format(new Date(dateStr));
}

export function formatTaxPeriod(taxableFrom: string, taxableTo: string): string {
  return `${formatDate(taxableFrom)} - ${formatDate(taxableTo)}`;
}

export function hasCustomTaxPeriod(year: number, taxableFrom: string): boolean {
  return !taxableFrom.startsWith(`${year}-01-01`);
}

export function numColor(value: number): string {
  if (value > 0.005) return 'text-green-400';
  if (value < -0.005) return 'text-red-400';
  return 'text-blue-400';
}
