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
  taxableFrom: string;
  taxableTo: string;
}

export interface Pit38Fields {
  year: number;
  // Section C
  poz22Przychody: number;
  poz23Koszty: number;
  poz24RazemPrzychody: number;
  poz25RazemKoszty: number;
  poz26Dochod: number;
  poz27Strata: number;
  // Section D
  Poz31PodstawaObliczenia: number;
  Poz33Podatek: number;
  Poz35PodatekNalezny: number;
  // Section G
  Poz47ZryczaltowanyPodatek: number;
  Poz48PodatekZaplaconyZaGranica: number;
  Poz49Roznica: number;
  Poz51PodatekDoZaplaty: number;
  // Informational
  totalDividendsPln: number;
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
