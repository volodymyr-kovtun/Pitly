import { useState, useMemo } from 'react';
import { ArrowUpDown, Search } from 'lucide-react';
import { formatPln, formatUsd, formatRate, formatDate, numColor } from '../format';
import type { AppState, TradeResult } from '../types';
import EmptyState from '../components/EmptyState';

type SortKey = 'dateTime' | 'symbol' | 'gainLossPln';

export default function TransactionsPage({ state }: { state: AppState }) {
  const [symbolFilter, setSymbolFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState<'all' | 'Buy' | 'Sell'>('all');
  const [sortKey, setSortKey] = useState<SortKey>('dateTime');
  const [sortAsc, setSortAsc] = useState(true);
  const [page, setPage] = useState(1);
  const pageSize = 25;

  const filtered = useMemo(() => {
    let data = [...state.trades];
    if (symbolFilter) data = data.filter(t => t.symbol.toLowerCase().includes(symbolFilter.toLowerCase()));
    if (typeFilter !== 'all') data = data.filter(t => t.type === typeFilter);

    data.sort((a, b) => {
      let cmp = 0;
      if (sortKey === 'dateTime') cmp = new Date(a.dateTime).getTime() - new Date(b.dateTime).getTime();
      else if (sortKey === 'symbol') cmp = a.symbol.localeCompare(b.symbol);
      else cmp = a.gainLossPln - b.gainLossPln;
      return sortAsc ? cmp : -cmp;
    });
    return data;
  }, [state.trades, symbolFilter, typeFilter, sortKey, sortAsc]);

  const totals = useMemo(() => {
    let proceeds = 0, cost = 0, gainLoss = 0;
    for (const t of filtered) {
      if (t.type !== 'Sell') continue;
      proceeds += t.proceedsPln;
      cost += t.costPln;
      gainLoss += t.gainLossPln;
    }
    return { proceeds, cost, gainLoss };
  }, [filtered]);

  if (!state.sessionId || !state.summary) {
    return <EmptyState />;
  }

  const totalPages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const paginated = filtered.slice((page - 1) * pageSize, page * pageSize);

  const toggleSort = (key: SortKey) => {
    if (sortKey === key) setSortAsc(!sortAsc);
    else { setSortKey(key); setSortAsc(true); }
  };

  const sortHeader = (label: string, k: SortKey) => (
    <th
      className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 cursor-pointer hover:text-slate-200 select-none"
      onClick={() => toggleSort(k)}
    >
      <span className="inline-flex items-center gap-1">
        {label}
        <ArrowUpDown className="w-3 h-3" />
      </span>
    </th>
  );

  return (
    <div className="space-y-6">
      <h1 className="text-white text-2xl font-bold">Transactions</h1>

      <div className="flex flex-wrap items-center gap-4">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
          <input
            type="text"
            placeholder="Filter by symbol..."
            value={symbolFilter}
            onChange={(e) => { setSymbolFilter(e.target.value); setPage(1); }}
            className="bg-slate-800 border border-slate-700 rounded-lg pl-9 pr-4 py-2 text-sm text-white placeholder:text-slate-500 focus:outline-none focus:border-blue-500"
          />
        </div>
        <div className="flex rounded-lg overflow-hidden border border-slate-700">
          {(['all', 'Buy', 'Sell'] as const).map(t => (
            <button
              key={t}
              onClick={() => { setTypeFilter(t); setPage(1); }}
              className={`px-4 py-2 text-sm transition-colors ${
                typeFilter === t ? 'bg-blue-600 text-white' : 'bg-slate-800 text-slate-400 hover:text-white'
              }`}
            >
              {t === 'all' ? 'All' : t}
            </button>
          ))}
        </div>
        <span className="text-slate-500 text-sm ml-auto">{filtered.length} transactions</span>
      </div>

      <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-700">
                {sortHeader("Date", "dateTime")}
                {sortHeader("Symbol", "symbol")}
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-left">Type</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Qty</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Price (USD)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Proceeds (USD)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Rate</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Proceeds (PLN)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Cost (PLN)</th>
                {sortHeader("Gain/Loss (PLN)", "gainLossPln")}
              </tr>
            </thead>
            <tbody>
              {paginated.map((t, i) => (
                <Row key={`${t.symbol}-${t.dateTime}-${i}`} trade={t} />
              ))}
            </tbody>
            <tfoot>
              <tr className="border-t border-slate-600 bg-slate-800/80">
                <td colSpan={7} className="px-3 py-3 text-slate-300 font-medium text-sm">Totals (sells)</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-blue-400 text-sm">{formatPln(totals.proceeds)}</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-blue-400 text-sm">{formatPln(totals.cost)}</td>
                <td className={`px-3 py-3 text-right font-mono tabular-nums text-sm ${numColor(totals.gainLoss)}`}>{formatPln(totals.gainLoss)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-center gap-2">
          <button
            disabled={page <= 1}
            onClick={() => setPage(p => p - 1)}
            className="px-3 py-1.5 text-sm rounded-lg bg-slate-800 border border-slate-700 text-slate-400 hover:text-white disabled:opacity-40"
          >
            Previous
          </button>
          <span className="text-slate-400 text-sm px-3">Page {page} of {totalPages}</span>
          <button
            disabled={page >= totalPages}
            onClick={() => setPage(p => p + 1)}
            className="px-3 py-1.5 text-sm rounded-lg bg-slate-800 border border-slate-700 text-slate-400 hover:text-white disabled:opacity-40"
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}

function Row({ trade: t }: { trade: TradeResult }) {
  const isSell = t.type === 'Sell';
  return (
    <tr className={`border-b border-slate-700/50 hover:bg-slate-700/30 transition-colors ${t.rateUnavailable ? 'bg-amber-500/5' : ''}`}>
      <td className="px-3 py-2.5 text-slate-300 whitespace-nowrap" title={t.rateUnavailable ? 'Rate unavailable — manual entry required' : undefined}>
        {formatDate(t.dateTime)}
      </td>
      <td className="px-3 py-2.5 text-white font-medium">{t.symbol}</td>
      <td className="px-3 py-2.5">
        <span className={t.type === 'Buy'
          ? 'bg-blue-500/20 text-blue-300 text-xs px-2 py-0.5 rounded-full'
          : 'bg-amber-500/20 text-amber-300 text-xs px-2 py-0.5 rounded-full'
        }>
          {t.type}
        </span>
      </td>
      <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-300">{t.quantity}</td>
      <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-300">{formatUsd(t.priceOriginal)}</td>
      <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-300">{isSell ? formatUsd(t.proceedsOriginal) : '\u2014'}</td>
      <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-400">{formatRate(t.exchangeRate)}</td>
      <td className="px-3 py-2.5 text-right font-mono tabular-nums text-blue-400">{isSell ? formatPln(t.proceedsPln) : '\u2014'}</td>
      <td className="px-3 py-2.5 text-right font-mono tabular-nums text-blue-400">{formatPln(t.costPln)}</td>
      <td className={`px-3 py-2.5 text-right font-mono tabular-nums ${isSell ? numColor(t.gainLossPln) : 'text-slate-600'}`}>
        {isSell ? formatPln(t.gainLossPln) : '\u2014'}
      </td>
    </tr>
  );
}
