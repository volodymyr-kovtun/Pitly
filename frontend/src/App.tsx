import { useState } from 'react';
import { Routes, Route, useNavigate } from 'react-router-dom';
import Sidebar from './components/Sidebar';
import ImportPage from './pages/ImportPage';
import DashboardPage from './pages/DashboardPage';
import TransactionsPage from './pages/TransactionsPage';
import DividendsPage from './pages/DividendsPage';
import Pit38Page from './pages/Pit38Page';
import type { AppState } from './types';

export default function App() {
  const navigate = useNavigate();
  const [state, setState] = useState<AppState>({
    sessionId: null,
    summary: null,
    trades: [],
    dividends: [],
  });

  const handleImportComplete = (data: AppState) => {
    setState(data);
    navigate('/dashboard');
  };

  return (
    <div className="flex min-h-screen">
      <Sidebar year={state.summary?.year ?? null} />
      <main className="flex-1 ml-16 p-8">
        <Routes>
          <Route path="/" element={<ImportPage onComplete={handleImportComplete} />} />
          <Route path="/dashboard" element={<DashboardPage state={state} />} />
          <Route path="/transactions" element={<TransactionsPage state={state} />} />
          <Route path="/dividends" element={<DividendsPage state={state} />} />
          <Route path="/pit38" element={<Pit38Page state={state} />} />
        </Routes>
      </main>
    </div>
  );
}
