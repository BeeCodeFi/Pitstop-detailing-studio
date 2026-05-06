import { useEffect, useState } from 'react';
import { employeeService, authService } from '../services/dataService';
import { useAuth } from '../context/AuthContext';
import DataTable from '../components/DataTable';
import Modal from '../components/Modal';
import LoadingSpinner from '../components/LoadingSpinner';
import toast from 'react-hot-toast';
import { Plus, Edit2, Trash2 } from 'lucide-react';

export default function EmployeesPage() {
  const { user } = useAuth();
  const [employees, setEmployees] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [showEditModal, setShowEditModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [createForm, setCreateForm] = useState({ name: '', username: '', password: '', role: 'Employee', phone: '' });
  const [editForm, setEditForm] = useState({ name: '', phone: '', role: '', isActive: true, newPassword: '' });

  useEffect(() => { loadEmployees(); }, []);

  const loadEmployees = async () => {
    setLoading(true);
    try {
      const { data } = await employeeService.getAll();
      setEmployees(data);
    } catch (err) {
      toast.error('Failed to load employees');
    } finally {
      setLoading(false);
    }
  };

  const openEdit = (emp) => {
    setEditing(emp);
    setEditForm({ name: emp.name, phone: emp.phone || '', role: emp.role, isActive: emp.isActive, newPassword: '' });
    setShowEditModal(true);
  };

  const handleCreate = async (e) => {
    e.preventDefault();
    try {
      await authService.register(createForm);
      toast.success('Employee created');
      setShowCreateModal(false);
      setCreateForm({ name: '', username: '', password: '', role: 'Employee', phone: '' });
      loadEmployees();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Failed to create employee');
    }
  };

  const handleEdit = async (e) => {
    e.preventDefault();
    try {
      await employeeService.update(editing.id, editForm);
      toast.success('Employee updated');
      setShowEditModal(false);
      loadEmployees();
    } catch (err) {
      toast.error('Failed to update employee');
    }
  };

  const handleDelete = async (emp) => {
    if (!window.confirm(`Delete "${emp.name}"?\nNote: Employees with existing daybook records cannot be deleted — deactivate them instead.`)) return;
    try {
      await employeeService.delete(emp.id);
      toast.success('Employee deleted');
      loadEmployees();
    } catch (err) {
      toast.error(err.response?.data?.message || 'Failed to delete employee');
    }
  };

  const columns = [
    { header: 'Name', accessor: 'name' },
    { header: 'Username', accessor: 'username' },
    { header: 'Role', render: r => (
      <span className={`text-xs px-2 py-1 rounded-full font-medium ${r.role === 'Admin' ? 'bg-purple-100 text-purple-700' : 'bg-blue-100 text-blue-700'}`}>
        {r.role}
      </span>
    )},
    { header: 'Phone', render: r => r.phone || '—' },
    { header: 'Status', render: r => (
      <span className={`text-xs px-2 py-1 rounded-full font-medium ${r.isActive ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`}>
        {r.isActive ? 'Active' : 'Inactive'}
      </span>
    )},
    { header: 'Actions', render: r => (
      <div className="flex items-center gap-2">
        <button onClick={() => openEdit(r)} className="text-primary hover:text-primary-dark cursor-pointer" title="Edit"><Edit2 className="w-4 h-4" /></button>
        {r.id !== user?.id && (
          <button onClick={() => handleDelete(r)} className="text-danger hover:text-red-700 cursor-pointer" title="Delete"><Trash2 className="w-4 h-4" /></button>
        )}
      </div>
    )},
  ];

  return (
    <div>
      <div className="flex items-center justify-between mb-6">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Employees</h2>
          <p className="text-sm text-gray-500 mt-1">Manage employee accounts</p>
        </div>
        <button onClick={() => setShowCreateModal(true)} className="flex items-center gap-1 bg-primary text-white px-4 py-2 rounded-lg text-sm hover:bg-primary-dark cursor-pointer">
          <Plus className="w-4 h-4" /> Add Employee
        </button>
      </div>

      {loading ? <LoadingSpinner message="Loading employees..." /> : (
        <DataTable columns={columns} data={employees} emptyMessage="No employees found" />
      )}

      {/* Create Modal */}
      <Modal isOpen={showCreateModal} onClose={() => setShowCreateModal(false)} title="Add Employee">
        <form onSubmit={handleCreate} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Full Name *</label>
            <input type="text" value={createForm.name} onChange={(e) => setCreateForm(p => ({ ...p, name: e.target.value }))} required className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Username *</label>
              <input type="text" value={createForm.username} onChange={(e) => setCreateForm(p => ({ ...p, username: e.target.value }))} required className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Password *</label>
              <input type="password" value={createForm.password} onChange={(e) => setCreateForm(p => ({ ...p, password: e.target.value }))} required minLength={6} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Role</label>
              <select value={createForm.role} onChange={(e) => setCreateForm(p => ({ ...p, role: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none">
                <option value="Employee">Employee</option>
                <option value="Admin">Admin</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
              <input type="text" value={createForm.phone} onChange={(e) => setCreateForm(p => ({ ...p, phone: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
            </div>
          </div>
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={() => setShowCreateModal(false)} className="px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg cursor-pointer">Cancel</button>
            <button type="submit" className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary-dark cursor-pointer">Create</button>
          </div>
        </form>
      </Modal>

      {/* Edit Modal */}
      <Modal isOpen={showEditModal} onClose={() => setShowEditModal(false)} title={`Edit: ${editing?.name}`}>
        <form onSubmit={handleEdit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Name</label>
            <input type="text" value={editForm.name} onChange={(e) => setEditForm(p => ({ ...p, name: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Role</label>
              <select value={editForm.role} onChange={(e) => setEditForm(p => ({ ...p, role: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none">
                <option value="Employee">Employee</option>
                <option value="Admin">Admin</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">Phone</label>
              <input type="text" value={editForm.phone} onChange={(e) => setEditForm(p => ({ ...p, phone: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
            </div>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">New Password (leave blank to keep)</label>
              <input type="password" value={editForm.newPassword} onChange={(e) => setEditForm(p => ({ ...p, newPassword: e.target.value }))} className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-primary outline-none" />
            </div>
            <div className="flex items-end">
              <label className="flex items-center gap-2 cursor-pointer">
                <input type="checkbox" checked={editForm.isActive} onChange={(e) => setEditForm(p => ({ ...p, isActive: e.target.checked }))} className="rounded" />
                <span className="text-sm text-gray-700">Active</span>
              </label>
            </div>
          </div>
          <div className="flex justify-end gap-3 pt-2">
            <button type="button" onClick={() => setShowEditModal(false)} className="px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 rounded-lg cursor-pointer">Cancel</button>
            <button type="submit" className="px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary-dark cursor-pointer">Update</button>
          </div>
        </form>
      </Modal>
    </div>
  );
}
