import { useEffect, useState } from 'react';
import { serviceTypeService } from '../services/dataService';
import DataTable from '../components/DataTable';
import Modal from '../components/Modal';
import toast from 'react-hot-toast';
import { Plus, Edit2, Trash2 } from 'lucide-react';

export default function ServicesPage() {
  const [services, setServices] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState({ name: '', defaultPrice: '' });

  useEffect(() => { loadServices(); }, []);

  const loadServices = async () => {
    setLoading(true);
    try {
      const { data } = await serviceTypeService.getAll(true);
      setServices(data);
    } catch (err) {
      toast.error('Failed to load services');
    } finally {
      setLoading(false);
    }
  };

  const openCreate = () => {
    setEditing(null);
    setForm({ name: '', defaultPrice: '' });
    setShowModal(true);
  };

  const openEdit = (svc) => {
    setEditing(svc);
    setForm({ name: svc.name, defaultPrice: svc.defaultPrice });
    setShowModal(true);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      if (editing) {
        await serviceTypeService.update(editing.id, { name: form.name, defaultPrice: Number(form.defaultPrice) });
        toast.success('Service updated');
      } else {
        await serviceTypeService.create({ name: form.name, defaultPrice: Number(form.defaultPrice) });
        toast.success('Service added');
      }
      setShowModal(false);
      loadServices();
    } catch (err) {
      toast.error('Failed to save service');
    }
  };

  const toggleActive = async (svc) => {
    try {
      await serviceTypeService.update(svc.id, { isActive: !svc.isActive });
      toast.success(svc.isActive ? 'Service deactivated' : 'Service activated');
      loadServices();
    } catch (err) {
      toast.error('Failed to update');
    }
  };

  const handleDelete = async (svc) => {
    if (!window.confirm(`Delete "${svc.name}"? This cannot be undone.\nNote: Services with existing sales cannot be deleted — deactivate them instead.`)) return;
    try {
      await serviceTypeService.delete(svc.id);
      toast.success('Service deleted');
      loadServices();
    } catch (err) {
      const msg = err.response?.data?.message || 'Failed to delete service';
      toast.error(msg);
    }
  };

  const columns = [
    { header: 'Service Name', accessor: 'name' },
    { header: 'Default Price', render: r => `₹${r.defaultPrice.toLocaleString('en-IN')}` },
    {
      header: 'Status', render: r => (
        <span className={`text-xs px-2 py-1 rounded-full font-medium ${r.isActive ? 'bg-green-100 text-green-700' : 'bg-gray-100 text-gray-500'}`}>
          {r.isActive ? 'Active' : 'Inactive'}
        </span>
      )
    },
    {
      header: 'Actions', render: r => (
        <div className="flex items-center gap-2">
          <button onClick={() => openEdit(r)} className="text-primary hover:text-primary-dark cursor-pointer" title="Edit"><Edit2 className="w-4 h-4" /></button>
          <button onClick={() => toggleActive(r)} className={`text-xs px-2 py-1 rounded cursor-pointer ${r.isActive ? 'text-danger hover:bg-red-50' : 'text-success hover:bg-green-50'}`}>
            {r.isActive ? 'Deactivate' : 'Activate'}
          </button>
          <button onClick={() => handleDelete(r)} className="text-danger hover:text-red-700 cursor-pointer" title="Delete"><Trash2 className="w-4 h-4" /></button>
        </div>
      )
    },
  ];

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Services</h2>
          <p className="text-sm text-gray-500 mt-1">Manage car detailing service catalog</p>
        </div>
        <button onClick={openCreate} className="flex items-center gap-1 bg-primary text-white px-4 py-2 rounded-lg text-sm hover:bg-primary-dark cursor-pointer">
          <Plus className="w-4 h-4" /> Add Service
        </button>
      </div>

      {loading ? <div className="text-center text-gray-400 py-12">Loading...</div> : (
        <DataTable columns={columns} data={services} emptyMessage="No services found" />
      )}

      <Modal isOpen={showModal} onClose={() => setShowModal(false)} title={editing ? 'Edit Service' : 'Add Service'}>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Service Name *</label>
            <input type="text" value={form.name} onChange={(e) => setForm(p => ({ ...p, name: e.target.value }))} required className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Default Price (₹) *</label>
            <input type="number" min="0" step="0.01" value={form.defaultPrice} onChange={(e) => setForm(p => ({ ...p, defaultPrice: e.target.value }))} required className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
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
