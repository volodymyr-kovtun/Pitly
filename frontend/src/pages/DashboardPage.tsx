import { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { TrendingUp, Coins, Landmark, CreditCard } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend } from 'recharts';
import { formatPln, numColor } from '../format';
import type { AppState } from '../types';

export default function DashboardPage({ state }: { state: AppState }) {
  const navigate = useNavigate();

  if (!state.sessionId || !state.summary) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[60vh] text-slate-400">
        <p className="mb-4">No data imported yet.</p>
        <button onClick={() => navigate('/')} className="bg-blue-600 hover:bg-blue-500 text-white font-medium px-4 py-2 rounded-lg transition-colors">
          Import Statement
        </button>
      </div>
    );
  }

  const { summary, trades } = state;
  const totalTaxOwed = summary.capitalGainTaxPln + summary.dividendTaxOwedPln;

  const monthlyData = useMemo(() => {
    const months: Record<string, { proceeds: number; costs: number }> = {};
    for (const t of trades) {
      if (t.type !== 'Sell') continue;
      const d = new Date(t.dateTime);
      const key = `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
      if (!months[key]) months[key] = { proceeds: 0, costs: 0 };
      months[key].proceeds += t.proceedsPln;
      months[key].costs += t.costPln;
    }
    return Object.entries(months)
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([month, data]) => ({
        month,
        Proceeds: Math.round(data.proceeds),
        Costs: Math.round(data.costs),
      }));
  }, [trades]);

  const pieData = [
    { name: 'Capital Gains Tax', value: Math.round(summary.capitalGainTaxPln * 100) / 100 },
    { name: 'Dividend Tax', value: Math.round(summary.dividendTaxOwedPln * 100) / 100 },
  ].filter(d => d.value > 0);

  const PIE_COLORS = ['#3b82f6', '#f59e0b'];

  const cards = [
    {
      label: 'Capital Gains',
      value: summary.capitalGainPln,
      sub: `Tax: ${formatPln(summary.capitalGainTaxPln)}`,
      icon: TrendingUp,
      color: numColor(summary.capitalGainPln),
    },
    {
      label: 'Dividends',
      value: summary.totalDividendsPln,
      sub: `${state.dividends.length} payments`,
      icon: Coins,
      color: 'text-blue-400',
    },
    {
      label: 'Tax Paid (US)',
      value: summary.totalWithholdingPln,
      sub: 'Withholding tax credited',
      icon: Landmark,
      color: 'text-amber-400',
    },
    {
      label: 'Tax Owed (PL)',
      value: totalTaxOwed,
      sub: `Due by April 30, ${summary.year + 1}`,
      icon: CreditCard,
      color: 'text-white',
      highlight: true,
    },
  ];

  return (
    <div className="space-y-8">
      <h1 className="text-white text-2xl font-bold">Dashboard — Tax Year {summary.year}</h1>

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
        {cards.map((c) => (
          <div
            key={c.label}
            className={`bg-slate-800 border rounded-xl p-6 ${
              c.highlight ? 'border-blue-500 bg-blue-500/5' : 'border-slate-700'
            }`}
          >
            <div className="flex items-center justify-between mb-3">
              <span className="text-slate-400 text-sm">{c.label}</span>
              <c.icon className={`w-5 h-5 ${c.color}`} />
            </div>
            <p className={`text-2xl font-mono tabular-nums font-semibold ${c.color}`}>
              {formatPln(c.value)}
            </p>
            <p className="text-slate-500 text-xs mt-1">{c.sub}</p>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="bg-slate-800 border border-slate-700 rounded-xl p-6">
          <h2 className="text-white text-lg font-semibold mb-4">Monthly Proceeds vs Costs</h2>
          {monthlyData.length > 0 ? (
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={monthlyData}>
                <XAxis dataKey="month" tick={{ fill: '#94a3b8', fontSize: 12 }} />
                <YAxis tick={{ fill: '#94a3b8', fontSize: 12 }} />
                <Tooltip
                  contentStyle={{ background: '#1e293b', border: '1px solid #334155', borderRadius: 8 }}
                  labelStyle={{ color: '#e2e8f0' }}
                />
                <Bar dataKey="Proceeds" fill="#3b82f6" radius={[4, 4, 0, 0]} />
                <Bar dataKey="Costs" fill="#64748b" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          ) : (
            <p className="text-slate-500 text-center py-12">No sell transactions to chart</p>
          )}
        </div>

        <div className="bg-slate-800 border border-slate-700 rounded-xl p-6">
          <h2 className="text-white text-lg font-semibold mb-4">Tax Breakdown</h2>
          {pieData.length > 0 ? (
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={pieData}
                  cx="50%"
                  cy="50%"
                  innerRadius={60}
                  outerRadius={100}
                  dataKey="value"
                  label={({ name, value }) => `${name}: ${value} PLN`}
                >
                  {pieData.map((_, i) => (
                    <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
                  ))}
                </Pie>
                <Legend />
                <Tooltip
                  contentStyle={{ background: '#1e293b', border: '1px solid #334155', borderRadius: 8 }}
                />
              </PieChart>
            </ResponsiveContainer>
          ) : (
            <p className="text-slate-500 text-center py-12">No tax owed</p>
          )}
        </div>
      </div>
    </div>
  );
}
