import { useState, useMemo } from 'react';
import { Info, Search } from 'lucide-react';
import { formatPln, formatUsd, formatRate, formatDate, formatTaxPeriod, hasCustomTaxPeriod } from '../format';
import type { AppState } from '../types';
import EmptyState from '../components/EmptyState';

const PL_TAX_RATE = 0.19;

export default function DividendsPage({ state }: { state: AppState }) {
  const [symbolFilter, setSymbolFilter] = useState('');

  const filtered = useMemo(() => {
    if (!symbolFilter) return state.dividends;
    return state.dividends.filter(d => d.symbol.toLowerCase().includes(symbolFilter.toLowerCase()));
  }, [state.dividends, symbolFilter]);

  const totals = useMemo(() => {
    let amountPln = 0, withholdingPln = 0, plTax = 0, netOwed = 0;
    for (const d of filtered) {
      const tax = d.amountPln * PL_TAX_RATE;
      amountPln += d.amountPln;
      withholdingPln += d.withholdingTaxPln;
      plTax += tax;
      netOwed += Math.max(tax - d.creditableWithholdingTaxPln, 0);
    }
    return { amountPln, withholdingPln, plTax, netOwed };
  }, [filtered]);

  if (!state.sessionId || !state.summary) {
    return <EmptyState />;
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-white text-2xl font-bold">Dividends</h1>
        <p className="text-slate-400 text-sm mt-1">
          Showing dividends received in {formatTaxPeriod(state.summary.taxableFrom, state.summary.taxableTo)}.
        </p>
        {hasCustomTaxPeriod(state.summary.year, state.summary.taxableFrom) && (
          <p className="text-slate-500 text-sm mt-1">
            Only dividends paid on or after the residency start date are included.
          </p>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-4">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
          <input
            type="text"
            placeholder="Filter by symbol..."
            value={symbolFilter}
            onChange={(e) => setSymbolFilter(e.target.value)}
            className="bg-slate-800 border border-slate-700 rounded-lg pl-9 pr-4 py-2 text-sm text-white placeholder:text-slate-500 focus:outline-none focus:border-blue-500"
          />
        </div>
        <span className="text-slate-500 text-sm ml-auto">{filtered.length} dividends</span>
      </div>

      <div className="flex items-start gap-3 bg-blue-500/10 border border-blue-500/30 rounded-xl p-4">
        <Info className="w-5 h-5 text-blue-400 shrink-0 mt-0.5" />
        <div className="text-sm text-blue-300">
          <p>Polish tax on dividends is 19%. Foreign withholding tax can be credited against it, but only up to the rate set by the bilateral tax treaty with the dividend&apos;s source country (art. 30a ust. 9). For US shares the cap is 15% under the PL-US treaty; other countries vary (e.g. PL-DK is also 15%, even though Denmark withholds 27% at source).</p>
          <p className="mt-1">Net tax owed = max(19% &times; gross dividend &minus; capped foreign tax, 0), summed per dividend.</p>
        </div>
      </div>

      <div className="bg-slate-800 border border-slate-700 rounded-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-700">
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-left">Date</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-left">Symbol</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Amount (USD)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Rate</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Amount (PLN)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Foreign Tax (PLN)</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">PL Tax 19%</th>
                <th className="text-slate-400 text-xs uppercase tracking-wider font-medium px-3 py-3 text-right">Net Tax Owed</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((d, i) => {
                const plTax = d.amountPln * PL_TAX_RATE;
                const netOwed = Math.max(plTax - d.creditableWithholdingTaxPln, 0);
                return (
                  <tr
                    key={i}
                    className={`border-b border-slate-700/50 hover:bg-slate-700/30 transition-colors ${d.rateUnavailable ? 'bg-amber-500/5' : ''}`}
                  >
                    <td className="px-3 py-2.5 text-slate-300 whitespace-nowrap">{formatDate(d.date)}</td>
                    <td className="px-3 py-2.5 text-white font-medium">{d.symbol}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-300">{formatUsd(d.amountOriginal)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-400">{formatRate(d.exchangeRate)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-blue-400">{formatPln(d.amountPln)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-red-400">{formatPln(d.withholdingTaxPln)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-slate-300">{formatPln(plTax)}</td>
                    <td className="px-3 py-2.5 text-right font-mono tabular-nums text-amber-400">{formatPln(netOwed)}</td>
                  </tr>
                );
              })}
            </tbody>
            <tfoot>
              <tr className="border-t border-slate-600 bg-slate-800/80">
                <td colSpan={4} className="px-3 py-3 text-slate-300 font-medium text-sm">Totals</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-blue-400 text-sm">{formatPln(totals.amountPln)}</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-red-400 text-sm">{formatPln(totals.withholdingPln)}</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-slate-300 text-sm">{formatPln(totals.plTax)}</td>
                <td className="px-3 py-3 text-right font-mono tabular-nums text-amber-400 text-sm">{formatPln(totals.netOwed)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </div>
    </div>
  );
}
