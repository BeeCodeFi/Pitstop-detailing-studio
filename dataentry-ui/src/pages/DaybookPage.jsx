import { useEffect, useState } from 'react';
import { useAuth } from '../context/AuthContext';
import { daybookService, customerService, serviceTypeService } from '../services/dataService';
import StatCard from '../components/StatCard';
import Modal from '../components/Modal';
import toast from 'react-hot-toast';
import {
  Plus, Trash2, Lock, IndianRupee, TrendingUp, Wallet,
  Receipt, CreditCard, Smartphone, Search, ChevronLeft, ChevronRight, CalendarDays, X
} from 'lucide-react';

export default function DaybookPage() {
  const { user, isAdmin, activeDaybookDate: date, setActiveDaybookDate: setDate } = useAuth();
  const [daybook, setDaybook] = useState(null);
  const [services, setServices] = useState([]);
  const [customers, setCustomers] = useState([]);
  const [customerSearch, setCustomerSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [showSaleModal, setShowSaleModal] = useState(false);
  const [showExpenseModal, setShowExpenseModal] = useState(false);

  const EMPTY_LINE = () => ({ serviceTypeId: '', amount: '' });
  const [saleForm, setSaleForm] = useState({
    customerId: '', vehicleNumber: '', vehicleType: 'Hatchback',
    paymentMode: 'Cash', notes: '',
    serviceLines: [EMPTY_LINE()]
  });
  const [expenseForm, setExpenseForm] = useState({ description: '', amount: '' });

  const today = new Date().toISOString().split('T')[0];
  const isToday = date === today;
  const isPastDate = date < today;

  // Reset to today when component unmounts (optional — keep commented to persist across sessions)

  const goToPreviousDay = () => {
    const d = new Date(date + 'T00:00:00');
    d.setDate(d.getDate() - 1);
    setDate(d.toISOString().split('T')[0]);
  };

  const goToNextDay = () => {
    if (isToday) return;
    const d = new Date(date + 'T00:00:00');
    d.setDate(d.getDate() + 1);
    setDate(d.toISOString().split('T')[0]);
  };

  const formattedDate = new Date(date + 'T00:00:00').toLocaleDateString('en-IN', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric'
  });

  useEffect(() => {
    loadDaybook();
    loadServices();
  }, [date]);

  const loadDaybook = async () => {
    setLoading(true);
    try {
      const { data } = await daybookService.get(date);
      setDaybook(data);
    } catch (err) {
      toast.error('Failed to load daybook');
    } finally {
      setLoading(false);
    }
  };

  const loadServices = async () => {
    try {
      const { data } = await serviceTypeService.getAll();
      setServices(data);
    } catch (err) { /* ignore */ }
  };

  const searchCustomers = async (query) => {
    setCustomerSearch(query);
    if (query.length < 2) { setCustomers([]); return; }
    try {
      const { data } = await customerService.getAll(query);
      setCustomers(data);
    } catch (err) { /* ignore */ }
  };

  const updateServiceLine = (index, field, value) => {
    setSaleForm(prev => {
      const lines = prev.serviceLines.map((l, i) => {
        if (i !== index) return l;
        if (field === 'serviceTypeId') {
          const svc = services.find(s => s.id === Number(value));
          return { ...l, serviceTypeId: value, amount: svc ? String(svc.defaultPrice) : l.amount };
        }
        return { ...l, [field]: value };
      });
      return { ...prev, serviceLines: lines };
    });
  };

  const addServiceLine = () => {
    setSaleForm(prev => ({ ...prev, serviceLines: [...prev.serviceLines, EMPTY_LINE()] }));
  };

  const removeServiceLine = (index) => {
    setSaleForm(prev => ({ ...prev, serviceLines: prev.serviceLines.filter((_, i) => i !== index) }));
  };

  const handleAddSale = async (e) => {
    e.preventDefault();
    const validLines = saleForm.serviceLines.filter(l => l.serviceTypeId && l.amount && Number(l.amount) > 0);
    if (validLines.length === 0) {
      toast.error('At least one service with a valid amount is required');
      return;
    }
    try {
      for (const line of validLines) {
        await daybookService.addSale(daybook.id, {
          customerId: saleForm.customerId ? Number(saleForm.customerId) : null,
          serviceTypeId: Number(line.serviceTypeId),
          vehicleNumber: saleForm.vehicleNumber || null,
          vehicleType: saleForm.vehicleType || null,
          amount: Number(line.amount),
          paymentMode: saleForm.paymentMode,
          notes: saleForm.notes || null
        });
      }
      toast.success(validLines.length > 1 ? `${validLines.length} sales added` : 'Sale added');
      setShowSaleModal(false);
      setSaleForm({ customerId: '', vehicleNumber: '', vehicleType: 'Hatchback', paymentMode: 'Cash', notes: '', serviceLines: [EMPTY_LINE()] });
      setCustomerSearch('');
      loadDaybook();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Failed to add sale');
    }
  };

  const handleDeleteSale = async (saleId) => {
    if (!confirm('Delete this sale?')) return;
    try {
      await daybookService.deleteSale(saleId);
      toast.success('Sale deleted');
      loadDaybook();
    } catch (err) {
      toast.error('Failed to delete sale');
    }
  };

  const handleAddExpense = async (e) => {
    e.preventDefault();
    const amountValue = Number(expenseForm.amount);
    if (!expenseForm.description || !expenseForm.amount || isNaN(amountValue) || amountValue <= 0) {
      toast.error('Description and a valid amount are required');
      return;
    }
    try {
      await daybookService.addExpense(daybook.id, {
        description: expenseForm.description,
        amount: amountValue
      });
      toast.success('Expense added');
      setShowExpenseModal(false);
      setExpenseForm({ description: '', amount: '' });
      loadDaybook();
    } catch (err) {
      toast.error('Failed to add expense');
    }
  };

  const handleDeleteExpense = async (expenseId) => {
    if (!confirm('Delete this expense?')) return;
    try {
      await daybookService.deleteExpense(expenseId);
      toast.success('Expense deleted');
      loadDaybook();
    } catch (err) {
      toast.error('Failed to delete expense');
    }
  };

  const handleFinalize = async () => {
    if (!confirm('Finalize this day? No more changes can be made after this.')) return;
    try {
      await daybookService.finalize(daybook.id);
      toast.success('Day finalized');
      loadDaybook();
    } catch (err) {
      toast.error('Failed to finalize');
    }
  };

  if (loading) {
    return <div className="flex items-center justify-center h-64 text-gray-400">Loading...</div>;
  }

  if (!daybook) return null;

  return (
    <div>
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-4">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Daily Daybook</h2>
          <p className="text-sm text-gray-500 mt-1">{daybook.employeeName}</p>
        </div>
        <div className="flex flex-col items-end gap-2">
          {/* Date Navigation */}
          <div className="flex items-center gap-2">
            <button
              onClick={goToPreviousDay}
              className="p-1.5 rounded-lg border border-gray-300 hover:bg-gray-100 transition-colors cursor-pointer"
              title="Previous day"
            >
              <ChevronLeft className="w-4 h-4 text-gray-600" />
            </button>
            <div className="relative">
              <CalendarDays className="absolute left-3 top-2.5 w-4 h-4 text-gray-400 pointer-events-none" />
              <input
                type="date"
                value={date}
                max={today}
                onChange={(e) => setDate(e.target.value)}
                className="pl-9 pr-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
              />
            </div>
            <button
              onClick={goToNextDay}
              disabled={isToday}
              className={`p-1.5 rounded-lg border border-gray-300 transition-colors ${
                isToday ? 'opacity-40 cursor-not-allowed' : 'hover:bg-gray-100 cursor-pointer'
              }`}
              title="Next day"
            >
              <ChevronRight className="w-4 h-4 text-gray-600" />
            </button>
            {!isToday && (
              <button
                onClick={() => setDate(today)}
                className="text-xs px-3 py-2 rounded-lg border border-primary text-primary hover:bg-primary hover:text-white transition-colors cursor-pointer font-medium"
              >
                Today
              </button>
            )}
            {daybook.isFinalized && (
              <span className="flex items-center gap-1 text-xs bg-green-100 text-green-700 px-3 py-1.5 rounded-full font-medium">
                <Lock className="w-3 h-3" /> Finalized
              </span>
            )}
          </div>
          {/* Date label */}
          <p className="text-xs text-gray-400">{formattedDate}</p>
        </div>
      </div>

      {/* Past date banner */}
      {isPastDate && (
        <div className={`flex items-center gap-2 px-4 py-2.5 rounded-lg text-sm mb-4 ${
          daybook.isFinalized
            ? 'bg-gray-100 border border-gray-300 text-gray-600'
            : 'bg-amber-50 border border-amber-200 text-amber-700'
        }`}>
          <CalendarDays className="w-4 h-4 flex-shrink-0" />
          {daybook.isFinalized
            ? 'This past date has been finalized and is locked. No further changes can be made.'
            : 'Viewing a past date — you can still add or remove sales and expenses for this date.'}
        </div>
      )}

      {/* Summary Cards */}
      <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3 mb-6">
        <StatCard title="Opening" value={daybook.openingBalance} icon={IndianRupee} color="primary" />
        <StatCard title="Total Sales" value={daybook.totalSales} icon={TrendingUp} color="success" />
        <StatCard title="Cash" value={daybook.totalCashCollected} icon={Wallet} color="success" />
        <StatCard title="Card" value={daybook.totalCardCollected} icon={CreditCard} color="accent" />
        <StatCard title="UPI" value={daybook.totalUpiCollected} icon={Smartphone} color="warning" />
        <StatCard title="Expenses" value={daybook.totalExpenses} icon={Receipt} color="danger" />
      </div>

      {/* Closing Balance */}
      <div className="bg-gradient-to-r from-primary to-primary-light rounded-xl p-5 mb-6 text-white">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-white/80">Closing Balance</p>
            <p className="text-3xl font-bold mt-1">₹{daybook.closingBalance.toLocaleString('en-IN')}</p>
            <p className="text-xs text-white/60 mt-1">Opening ({daybook.openingBalance}) + Sales ({daybook.totalSales}) - Expenses ({daybook.totalExpenses})</p>
          </div>
          {!daybook.isFinalized && isAdmin && (
            <button
              onClick={handleFinalize}
              className="flex items-center gap-2 bg-white/20 hover:bg-white/30 text-white px-4 py-2 rounded-lg text-sm font-medium transition-colors cursor-pointer"
            >
              <Lock className="w-4 h-4" /> Finalize Day
            </button>
          )}
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Sales Section */}
        <div className="bg-white rounded-xl border border-gray-200">
          <div className="flex items-center justify-between p-4 border-b border-gray-200">
            <h3 className="font-semibold text-gray-900">Sales ({daybook.sales?.length || 0})</h3>
            {(!daybook.isFinalized || isAdmin) && (
              <button
                onClick={() => setShowSaleModal(true)}
                className="flex items-center gap-1 bg-primary text-white px-3 py-1.5 rounded-lg text-sm hover:bg-primary-dark transition-colors cursor-pointer"
              >
                <Plus className="w-4 h-4" /> Add Sale
              </button>
            )}
          </div>
          <div className="divide-y divide-gray-50 max-h-96 overflow-auto">
            {daybook.sales?.length === 0 && (
              <p className="p-8 text-center text-gray-400 text-sm">No sales yet. Click "Add Sale" to start.</p>
            )}
            {daybook.sales?.map(sale => (
              <div key={sale.id} className="flex items-center justify-between p-4 hover:bg-gray-50">
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <p className="text-sm font-medium text-gray-700">{sale.serviceTypeName}</p>
                    <span className={`text-xs px-2 py-0.5 rounded-full ${
                      sale.paymentMode === 'Cash' ? 'bg-green-100 text-green-700' :
                      sale.paymentMode === 'Card' ? 'bg-blue-100 text-blue-700' :
                      'bg-yellow-100 text-yellow-700'
                    }`}>
                      {sale.paymentMode}
                    </span>
                  </div>
                  <p className="text-xs text-gray-400 mt-0.5">
                    {sale.customerName || 'Walk-in'}
                    {sale.vehicleNumber ? ` • ${sale.vehicleNumber}` : ''}
                    {sale.vehicleType ? ` • ${sale.vehicleType}` : ''}
                  </p>
                </div>
                <div className="flex items-center gap-3">
                  <p className="text-sm font-bold text-gray-900">₹{sale.amount.toLocaleString('en-IN')}</p>
                  {isAdmin && (
                    <button onClick={() => handleDeleteSale(sale.id)} className="text-gray-300 hover:text-danger cursor-pointer">
                      <Trash2 className="w-4 h-4" />
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
          {daybook.sales?.length > 0 && (
            <div className="p-4 border-t border-gray-200 bg-gray-50 rounded-b-xl">
              <div className="flex justify-between text-sm font-semibold text-gray-700">
                <span>Total Sales</span>
                <span>₹{daybook.totalSales.toLocaleString('en-IN')}</span>
              </div>
            </div>
          )}
        </div>

        {/* Expenses Section */}
        <div className="bg-white rounded-xl border border-gray-200">
          <div className="flex items-center justify-between p-4 border-b border-gray-200">
            <h3 className="font-semibold text-gray-900">Expenses ({daybook.expenses?.length || 0})</h3>
            {(!daybook.isFinalized || isAdmin) && (
              <button
                onClick={() => setShowExpenseModal(true)}
                className="flex items-center gap-1 bg-danger text-white px-3 py-1.5 rounded-lg text-sm hover:bg-red-600 transition-colors cursor-pointer"
              >
                <Plus className="w-4 h-4" /> Add Expense
              </button>
            )}
          </div>
          <div className="divide-y divide-gray-50 max-h-96 overflow-auto">
            {daybook.expenses?.length === 0 && (
              <p className="p-8 text-center text-gray-400 text-sm">No expenses recorded.</p>
            )}
            {daybook.expenses?.map(expense => (
              <div key={expense.id} className="flex items-center justify-between p-4 hover:bg-gray-50">
                <p className="text-sm text-gray-700">{expense.description}</p>
                <div className="flex items-center gap-3">
                  <p className="text-sm font-bold text-danger">-₹{expense.amount.toLocaleString('en-IN')}</p>
                  {isAdmin && (
                    <button onClick={() => handleDeleteExpense(expense.id)} className="text-gray-300 hover:text-danger cursor-pointer">
                      <Trash2 className="w-4 h-4" />
                    </button>
                  )}
                </div>
              </div>
            ))}
          </div>
          {daybook.expenses?.length > 0 && (
            <div className="p-4 border-t border-gray-200 bg-gray-50 rounded-b-xl">
              <div className="flex justify-between text-sm font-semibold text-danger">
                <span>Total Expenses</span>
                <span>-₹{daybook.totalExpenses.toLocaleString('en-IN')}</span>
              </div>
            </div>
          )}
        </div>
      </div>

      {/* Add Sale Modal */}
      <Modal isOpen={showSaleModal} onClose={() => { setShowSaleModal(false); setCustomerSearch(''); setSaleForm({ customerId: '', vehicleNumber: '', vehicleType: 'Hatchback', paymentMode: 'Cash', notes: '', serviceLines: [EMPTY_LINE()] }); }} title="Add Sale" size="md">
        <form onSubmit={handleAddSale} className="space-y-4">
          {/* Customer search */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Customer (optional)</label>
            <div className="relative">
              <Search className="absolute left-3 top-2.5 w-4 h-4 text-gray-400" />
              <input
                type="text"
                value={customerSearch}
                onChange={(e) => searchCustomers(e.target.value)}
                placeholder="Search by name, phone, or vehicle..."
                className="w-full pl-9 pr-4 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
              />
            </div>
            {customers.length > 0 && (
              <div className="mt-1 border border-gray-200 rounded-lg max-h-32 overflow-auto bg-white shadow-lg">
                {customers.map(c => (
                  <button
                    key={c.id}
                    type="button"
                    onClick={() => {
                      setSaleForm(prev => ({
                        ...prev,
                        customerId: c.id,
                        vehicleNumber: c.vehicleNumber || prev.vehicleNumber,
                        vehicleType: c.vehicleType || prev.vehicleType
                      }));
                      setCustomerSearch(c.name);
                      setCustomers([]);
                    }}
                    className="w-full text-left px-3 py-2 text-sm hover:bg-gray-50 cursor-pointer"
                  >
                    <span className="font-medium">{c.name}</span>
                    <span className="text-gray-400 ml-2">{c.phone} {c.vehicleNumber}</span>
                  </button>
                ))}
              </div>
            )}
          </div>

          {/* Service lines */}
          <div>
            <div className="flex items-center justify-between mb-1">
              <label className="block text-sm font-medium text-gray-700">Services *</label>
              <button
                type="button"
                onClick={addServiceLine}
                className="flex items-center gap-1 text-xs text-primary hover:text-primary-dark font-medium cursor-pointer"
              >
                <Plus className="w-3 h-3" /> Add service
              </button>
            </div>
            <div className="space-y-2">
              {saleForm.serviceLines.map((line, idx) => (
                <div key={idx} className="flex items-center gap-2">
                  <select
                    value={line.serviceTypeId}
                    onChange={(e) => updateServiceLine(idx, 'serviceTypeId', e.target.value)}
                    className="flex-1 px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
                  >
                    <option value="">Select service</option>
                    {services.map(s => (
                      <option key={s.id} value={s.id}>{s.name} (₹{s.defaultPrice})</option>
                    ))}
                  </select>
                  <div className="relative w-28 flex-shrink-0">
                    <span className="absolute left-2.5 top-2 text-gray-400 text-sm">₹</span>
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      value={line.amount}
                      onChange={(e) => updateServiceLine(idx, 'amount', e.target.value)}
                      placeholder="0"
                      className="w-full pl-6 pr-2 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
                    />
                  </div>
                  {saleForm.serviceLines.length > 1 && (
                    <button
                      type="button"
                      onClick={() => removeServiceLine(idx)}
                      className="p-1.5 text-gray-300 hover:text-danger cursor-pointer flex-shrink-0"
                    >
                      <X className="w-4 h-4" />
                    </button>
                  )}
                </div>
              ))}
            </div>
            {/* Total */}
            {saleForm.serviceLines.length > 1 && (
              <div className="flex justify-end mt-2 text-sm font-semibold text-gray-700">
                Total: ₹{saleForm.serviceLines.reduce((sum, l) => sum + (Number(l.amount) || 0), 0).toLocaleString('en-IN')}
              </div>
            )}
          </div>

          <div className="grid grid-cols-3 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Vehicle No.</label>
              <input
                type="text"
                value={saleForm.vehicleNumber}
                onChange={(e) => setSaleForm(prev => ({ ...prev, vehicleNumber: e.target.value }))}
                placeholder="KA01AB1234"
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Vehicle Type</label>
              <select
                value={saleForm.vehicleType}
                onChange={(e) => setSaleForm(prev => ({ ...prev, vehicleType: e.target.value }))}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
              >
                <option value="Hatchback">Hatchback</option>
                <option value="Sedan">Sedan</option>
                <option value="SUV">SUV</option>
                <option value="MUV">MUV</option>
                <option value="Crossover">Crossover</option>
                <option value="Convertible">Convertible</option>
                <option value="Bike">Bike</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Payment *</label>
              <select
                value={saleForm.paymentMode}
                onChange={(e) => setSaleForm(prev => ({ ...prev, paymentMode: e.target.value }))}
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
              >
                <option value="Cash">Cash</option>
                <option value="Card">Card</option>
                <option value="UPI">UPI</option>
              </select>
            </div>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Notes</label>
            <input
              type="text"
              value={saleForm.notes}
              onChange={(e) => setSaleForm(prev => ({ ...prev, notes: e.target.value }))}
              placeholder="Optional notes..."
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
            />
          </div>

          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={() => { setShowSaleModal(false); setCustomerSearch(''); setSaleForm({ customerId: '', vehicleNumber: '', vehicleType: 'Hatchback', paymentMode: 'Cash', notes: '', serviceLines: [EMPTY_LINE()] }); }} className="px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg cursor-pointer">Cancel</button>
            <button type="submit" className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary-dark cursor-pointer">
              {saleForm.serviceLines.filter(l => l.serviceTypeId).length > 1 ? `Add ${saleForm.serviceLines.filter(l => l.serviceTypeId).length} Sales` : 'Add Sale'}
            </button>
          </div>
        </form>
      </Modal>

      {/* Add Expense Modal */}
      <Modal isOpen={showExpenseModal} onClose={() => setShowExpenseModal(false)} title="Add Expense">
        <form onSubmit={handleAddExpense} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Description *</label>
            <input
              type="text"
              value={expenseForm.description}
              onChange={(e) => setExpenseForm(prev => ({ ...prev, description: e.target.value }))}
              required
              placeholder="e.g., Cleaning supplies, Lunch, Fuel"
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Amount *</label>
            <input
              type="number"
              min="0"
              step="0.01"
              value={expenseForm.amount}
              onChange={(e) => setExpenseForm(prev => ({ ...prev, amount: e.target.value }))}
              required
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
            />
          </div>
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={() => setShowExpenseModal(false)} className="px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg cursor-pointer">Cancel</button>
            <button type="submit" className="px-4 py-2 text-sm bg-danger text-white rounded-lg hover:bg-red-600 cursor-pointer">Add Expense</button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
