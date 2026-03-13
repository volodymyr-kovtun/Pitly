export interface TradeResult {
  id: number;
  symbol: string;
  dateTime: string;
  type: 'Buy' | 'Sell';
  quantity: number;
  priceOriginal: number;
  proceedsOriginal: number;
  commissionOriginal: number;
  currency: string;
  exchangeRate: number;
  proceedsPln: number;
  costPln: number;
  gainLossPln: number;
  rateUnavailable: boolean;
}

export interface DividendResult {
  id: number;
  symbol: string;
  currency: string;
  date: string;
  amountOriginal: number;
  withholdingTaxOriginal: number;
  amountPln: number;
  withholdingTaxPln: number;
  exchangeRate: number;
  rateUnavailable: boolean;
}

export interface TaxSummary {
  totalProceedsPln: number;
  totalCostPln: number;
  capitalGainPln: number;
  capitalGainTaxPln: number;
  totalDividendsPln: number;
  totalWithholdingPln: number;
  dividendTaxOwedPln: number;
  year: number;
}

export interface Pit38Fields {
  year: number;
  c20_Przychody: number;
  c21_Koszty: number;
  c22_DochodStrata: number;
  c23_PodstawaObliczenia: number;
  c24_Podatek19: number;
  d25_PrzychodyDywidendy: number;
  d26_ZryczaltowanyPodatek19: number;
  e27_PodatekZaplaconyZagranica: number;
  e28_PodatekDoZaplaty: number;
  totalTaxOwed: number;
}

export interface ImportResponse {
  sessionId: string;
  summary: TaxSummary;
  trades: TradeResult[];
  dividends: DividendResult[];
}

export interface TradesPage {
  items: TradeResult[];
  total: number;
  page: number;
  pageSize: number;
}

export interface AppState {
  sessionId: string | null;
  summary: TaxSummary | null;
  trades: TradeResult[];
  dividends: DividendResult[];
}
