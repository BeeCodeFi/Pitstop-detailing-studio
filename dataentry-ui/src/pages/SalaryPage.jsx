import { useEffect, useState } from 'react';
import { salaryService, employeeService } from '../services/dataService';
import DataTable from '../components/DataTable';
import Modal from '../components/Modal';
import toast from 'react-hot-toast';
import { Plus, Edit2, Trash2, IndianRupee } from 'lucide-react';
import StatCard from '../components/StatCard';

const EMPTY_FORM = { employeeId: '', amount: '', date: new Date().toISOString().split('T')[0], notes: '' };

export default function SalaryPage() {
  const [payments, setPayments] = useState([]);
  const [employees, setEmployees] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filterMonth, setFilterMonth] = useState(new Date().toISOString().slice(0, 7));
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY_FORM);

  useEffect(() => {
    employeeService.getAll().then(r => setEmployees(r.data)).catch(() => {});
  }, []);

  useEffect(() => {
    loadPayments();
  }, [filterMonth]);

  const loadPayments = async () => {
    setLoading(true);
    try {
      const [y, m] = filterMonth.split('-');
      const { data } = await salaryService.getAll(Number(y), Number(m));
      setPayments(data);
    } catch {
      toast.error('Failed to load salary payments');
    } finally {
      setLoading(false);
    }
  };

  const openCreate = () => {
    setEditing(null);
    setForm({ ...EMPTY_FORM, employeeId: employees[0]?.id?.toString() || '' });
    setShowModal(true);
  };

  const openEdit = (payment) => {
    setEditing(payment);
    setForm({
      employeeId: payment.employeeId.toString(),
      amount: payment.amount.toString(),
      date: payment.date,
      notes: payment.notes || '',
    });
    setShowModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    const payload = {
      employeeId: Number(form.employeeId),
      amount: parseFloat(form.amount),
      date: form.date,
      notes: form.notes || null,
    };
    try {
      if (editing) {
        await salaryService.update(editing.id, payload);
        toast.success('Salary payment updated');
      } else {
        await salaryService.create(payload);
        toast.success('Salary payment added');
      }
      setShowModal(false);
      loadPayments();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Failed to save salary payment');
    }
  };

  const handleDelete = async (payment) => {
    if (!window.confirm(`Delete salary payment of ₹${payment.amount.toLocaleString('en-IN')} for ${payment.employeeName}?`)) return;
    try {
      await salaryService.delete(payment.id);
      toast.success('Salary payment deleted');
      loadPayments();
    } catch {
      toast.error('Failed to delete salary payment');
    }
  };

  const totalSalary = payments.reduce((sum, p) => sum + p.amount, 0);

  const columns = [
    { header: 'Employee', accessor: 'employeeName' },
    {
      header: 'Date',
      render: r => new Date(r.date + 'T00:00:00').toLocaleDateString('en-IN', {
        day: '2-digit', month: 'short', year: 'numeric'
      })
    },
    { header: 'Amount', render: r => <span className="font-medium text-gray-900">₹{r.amount.toLocaleString('en-IN')}</span> },
    { header: 'Notes', render: r => r.notes || '—' },
    {
      header: 'Actions', render: r => (
        <div className="flex items-center gap-2">
          <button onClick={() => openEdit(r)} className="text-primary hover:text-primary-dark cursor-pointer" title="Edit">
            <Edit2 className="w-4 h-4" />
          </button>
          <button onClick={() => handleDelete(r)} className="text-danger hover:text-red-700 cursor-pointer" title="Delete">
            <Trash2 className="w-4 h-4" />
          </button>
        </div>
      )
    },
  ];

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Salary Payments</h2>
          <p className="text-sm text-gray-500 mt-1">Track salary disbursements to employees</p>
        </div>
        <button
          onClick={openCreate}
          className="flex items-center gap-1 bg-primary text-white px-4 py-2 rounded-lg text-sm hover:bg-primary-dark cursor-pointer"
        >
          <Plus className="w-4 h-4" /> Add Payment
        </button>
      </div>

      {/* Filter */}
      <div className="flex items-center gap-3 mb-6">
        <label className="text-sm font-medium text-gray-600">Month:</label>
        <input
          type="month"
          value={filterMonth}
          onChange={(e) => setFilterMonth(e.target.value)}
          className="px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
        />
      </div>

      {/* Summary */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-6">
        <StatCard
          title="Total Salary Paid"
          value={totalSalary}
          icon={IndianRupee}
          color="danger"
        />
        <div className="bg-white rounded-xl border border-gray-200 p-5 hover:shadow-md transition-shadow">
          <div className="flex items-center justify-between mb-3">
            <p className="text-sm font-medium text-gray-500">Payments This Month</p>
            <div className="p-2 rounded-lg bg-primary/10 text-primary">
              <IndianRupee className="w-5 h-5" />
            </div>
          </div>
          <p className="text-2xl font-bold text-gray-900">{payments.length}</p>
        </div>
      </div>

      {loading
        ? <div className="text-center text-gray-400 py-12">Loading...</div>
        : <DataTable columns={columns} data={payments} emptyMessage="No salary payments for this month" />
      }

      {/* Add / Edit Modal */}
      <Modal
        isOpen={showModal}
        onClose={() => setShowModal(false)}
        title={editing ? 'Edit Salary Payment' : 'Add Salary Payment'}
      >
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Employee *</label>
            <select
              value={form.employeeId}
              onChange={(e) => setForm(p => ({ ...p, employeeId: e.target.value }))}
              required
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
            >
              <option value="">Select employee…</option>
              {employees.map(emp => (
                <option key={emp.id} value={emp.id}>{emp.name}</option>
              ))}
            </select>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Amount (₹) *</label>
              <input
                type="number"
                min="1"
                step="0.01"
                value={form.amount}
                onChange={(e) => setForm(p => ({ ...p, amount: e.target.value }))}
                required
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Date *</label>
              <input
                type="date"
                value={form.date}
                onChange={(e) => setForm(p => ({ ...p, date: e.target.value }))}
                required
                className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Notes</label>
            <input
              type="text"
              value={form.notes}
              onChange={(e) => setForm(p => ({ ...p, notes: e.target.value }))}
              placeholder="e.g. April salary, advance…"
              className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none"
            />
          </div>
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg cursor-pointer">Cancel</button>
            <button type="submit" className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary-dark cursor-pointer">
              {editing ? 'Update' : 'Add'}
            </button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
