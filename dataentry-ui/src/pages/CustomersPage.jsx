import { useEffect, useState } from 'react';
import { customerService } from '../services/dataService';
import { useAuth } from '../context/AuthContext';
import DataTable from '../components/DataTable';
import Modal from '../components/Modal';
import toast from 'react-hot-toast';
import { Plus, Search, Edit2, Trash2 } from 'lucide-react';

export default function CustomersPage() {
  const { isAdmin } = useAuth();
  const [customers, setCustomers] = useState([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ name: '', phone: '', vehicleNumber: '', vehicleType: 'Car', notes: '' });

  useEffect(() => { loadCustomers(); }, []);

  const loadCustomers = async (q) => {
    setLoading(true);
    try {
      const { data } = await customerService.getAll(q || search);
      setCustomers(data);
    } catch (err) {
      toast.error('Failed to load customers');
    } finally {
      setLoading(false);
    }
  };

  const handleSearch = (value) => {
    setSearch(value);
    loadCustomers(value);
  };

  const openCreate = () => {
    setEditing(null);
    setForm({ name: '', phone: '', vehicleNumber: '', vehicleType: 'Car', notes: '' });
    setShowModal(true);
  };

  const openEdit = (customer) => {
    setEditing(customer);
    setForm({
      name: customer.name,
      phone: customer.phone || '',
      vehicleNumber: customer.vehicleNumber || '',
      vehicleType: customer.vehicleType || 'Car',
      notes: customer.notes || ''
    });
    setShowModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editing) {
        await customerService.update(editing.id, form);
        toast.success('Customer updated');
      } else {
        await customerService.create(form);
        toast.success('Customer added');
      }
      setShowModal(false);
      loadCustomers();
    } catch (err) {
      toast.error('Failed to save customer');
    }
  };

  const handleDelete = async (customer) => {
    if (!window.confirm(`Delete "${customer.name}"? Their linked sale records will become anonymous but won't be deleted.`)) return;
    try {
      await customerService.delete(customer.id);
      toast.success('Customer deleted');
      loadCustomers();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Failed to delete customer');
    }
  };

  const columns = [
    { header: 'Name', accessor: 'name' },
    { header: 'Phone', accessor: 'phone', render: (r) => r.phone || '—' },
    { header: 'Vehicle No.', accessor: 'vehicleNumber', render: (r) => r.vehicleNumber || '—' },
    { header: 'Type', accessor: 'vehicleType', render: (r) => r.vehicleType || '—' },
    {
      header: 'Actions', render: (r) => (
        <div className="flex items-center gap-2">
          <button onClick={() => openEdit(r)} className="text-primary hover:text-primary-dark cursor-pointer" title="Edit"><Edit2 className="w-4 h-4" /></button>
          {isAdmin && (
            <button onClick={() => handleDelete(r)} className="text-danger hover:text-red-700 cursor-pointer" title="Delete"><Trash2 className="w-4 h-4" /></button>
          )}
        </div>
      )
    },
  ];

  return (
    <div>
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-6">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Customers</h2>
          <p className="text-sm text-gray-500 mt-1">{customers.length} customers</p>
        </div>
        <div className="flex items-center gap-3">
          <div className="relative">
            <Search className="absolute left-3 top-2.5 w-4 h-4 text-gray-400" />
            <input
              type="text"
              value={search}
              onChange={(e) => handleSearch(e.target.value)}
              placeholder="Search name, phone, vehicle..."
              className="pl-9 pr-4 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none w-64"
            />
          </div>
          <button onClick={openCreate} className="flex items-center gap-1 bg-primary text-white px-4 py-2 rounded-lg text-sm hover:bg-primary-dark cursor-pointer">
            <Plus className="w-4 h-4" /> Add Customer
          </button>
        </div>
      </div>

      {loading ? (
        <div className="text-center text-gray-400 py-12">Loading...</div>
      ) : (
        <DataTable columns={columns} data={customers} emptyMessage="No customers found" />
      )}

      <Modal isOpen={showModal} onClose={() => setShowModal(false)} title={editing ? 'Edit Customer' : 'Add Customer'}>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Name *</label>
            <input type="text" value={form.name} onChange={(e) => setForm(p => ({ ...p, name: e.target.value }))} required className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
              <input type="text" value={form.phone} onChange={(e) => setForm(p => ({ ...p, phone: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Vehicle Number</label>
              <input type="text" value={form.vehicleNumber} onChange={(e) => setForm(p => ({ ...p, vehicleNumber: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Vehicle Type</label>
              <select value={form.vehicleType} onChange={(e) => setForm(p => ({ ...p, vehicleType: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none">
                <option value="Car">Car</option>
                <option value="SUV">SUV</option>
                <option value="Bike">Bike</option>
                <option value="Truck">Truck</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Notes</label>
              <input type="text" value={form.notes} onChange={(e) => setForm(p => ({ ...p, notes: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
            </div>
          </div>
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={() => setShowModal(false)} className="px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg cursor-pointer">Cancel</button>
            <button type="submit" className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary-dark cursor-pointer">{editing ? 'Update' : 'Add'}</button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
