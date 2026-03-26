import type { ImportResponse, TradesPage, DividendResult, TaxSummary } from './types';

const BASE = '/api';

export async function importFile(file: File): Promise<ImportResponse> {
  const form = new FormData();
  form.append('files', file);
  const res = await fetch(`${BASE}/import`, { method: 'POST', body: form });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: 'Import failed' }));
    throw new Error(err.error || 'Import failed');
  }
  return res.json();
}

export interface GiftedLotOverride {
  symbol: string;
  date: string;    // yyyy-MM-dd
  price: number;
  currency: string;
}

export async function importFiles(
  files: File[],
  assumeGiftedShares = false,
  giftedLotOverride?: GiftedLotOverride,
): Promise<ImportResponse> {
  const form = new FormData();
  files.forEach(file => form.append('files', file));
  if (assumeGiftedShares) {
    form.append('assumeGiftedShares', 'true');
    if (giftedLotOverride) {
      form.append('giftedSymbol', giftedLotOverride.symbol);
      form.append('giftedDate', giftedLotOverride.date);
      form.append('giftedPrice', String(giftedLotOverride.price));
      form.append('giftedCurrency', giftedLotOverride.currency);
    }
  }
  const res = await fetch(`${BASE}/import`, { method: 'POST', body: form });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ error: 'Import failed' }));
    throw new Error(err.error || 'Import failed');
  }
  return res.json();
}

export async function getTrades(
  sessionId: string,
  page = 1,
  pageSize = 25,
  sortBy?: string,
  sortOrder?: string,
  symbolFilter?: string,
): Promise<TradesPage> {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
  if (sortBy) params.set('sortBy', sortBy);
  if (sortOrder) params.set('sortOrder', sortOrder);
  if (symbolFilter) params.set('symbolFilter', symbolFilter);
  const res = await fetch(`${BASE}/session/${sessionId}/trades?${params}`);
  if (!res.ok) throw new Error('Failed to load trades');
  return res.json();
}

export async function getDividends(sessionId: string): Promise<DividendResult[]> {
  const res = await fetch(`${BASE}/session/${sessionId}/dividends`);
  if (!res.ok) throw new Error('Failed to load dividends');
  return res.json();
}

export async function getSummary(sessionId: string): Promise<TaxSummary> {
  const res = await fetch(`${BASE}/session/${sessionId}/summary`);
  if (!res.ok) throw new Error('Failed to load summary');
  return res.json();
}

export function getExportCsvUrl(sessionId: string): string {
  return `${BASE}/session/${sessionId}/export/csv`;
}
