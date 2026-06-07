import api from './api';
import {
  mockCustomers, mockServiceTypes, mockEmployees,
  mockDaybookEntry, mockAllSales, mockDailyReport,
  mockMonthlyReport, mockEmployeeReport, mockInsights,
  mockSalaryPayments,
} from './mockData';

// ── Explorer-mode helpers ────────────────────────────────────────────────────
const isExplorerMode = () => {
  try { return JSON.parse(localStorage.getItem('user'))?.role === 'Explorer'; }
  catch { return false; }
};

// Wrap data in an axios-like response object
const mock = (data) => Promise.resolve({ data });
const mockOk = () => Promise.resolve({ data: null, status: 200 });

// Incrementing ID for newly "created" demo items within the session
let _mockId = 9500;
const nextId = () => ++_mockId;

export const authService = {
  login:    (username, password) => api.post('/auth/login', { username, password }),
  register: (data) => {
    if (isExplorerMode()) {
      const emp = { id: nextId(), name: data.name, username: data.username, role: data.role ?? 'Employee', phone: data.phone ?? null, isActive: true, createdAt: new Date().toISOString() };
      return mock(emp);
    }
    return api.post('/auth/register', data);
  },
};

export const daybookService = {
  get: (date, employeeId) => {
    if (isExplorerMode()) return mock(mockDaybookEntry(date ?? new Date().toISOString().split('T')[0]));
    return api.get('/daybook', { params: { date, employeeId } });
  },
  getAllSales: (date) => {
    if (isExplorerMode()) return mock(mockAllSales(date ?? new Date().toISOString().split('T')[0]));
    return api.get('/daybook/all-sales', { params: { date } });
  },
  updateOpeningBalance: (id, openingBalance) => {
    if (isExplorerMode()) return mock({ ...mockDaybookEntry(new Date().toISOString().split('T')[0]), openingBalance });
    return api.put(`/daybook/${id}/opening-balance`, { openingBalance });
  },
  addSale: (id, sale) => {
    if (isExplorerMode()) {
      const svcName = mockServiceTypes.find(s => s.id === Number(sale.serviceTypeId))?.name ?? '';
      const custName = mockCustomers.find(c => c.id === Number(sale.customerId))?.name ?? null;
      return mock({ id: nextId(), customerId: sale.customerId ?? null, customerName: custName, serviceTypeId: sale.serviceTypeId, serviceTypeName: svcName, vehicleNumber: sale.vehicleNumber ?? null, vehicleType: sale.vehicleType ?? null, amount: sale.amount, paymentMode: sale.paymentMode, notes: sale.notes ?? null, createdAt: new Date().toISOString(), vehicleVisitCount: 1 });
    }
    return api.post(`/daybook/${id}/sales`, sale);
  },
  updateSale: (saleId, data) => {
    if (isExplorerMode()) {
      const existing = mockDaybookEntry('').sales.find(s => s.id === Number(saleId)) ?? {};
      return mock({ ...existing, ...data, id: saleId });
    }
    return api.put(`/daybook/sales/${saleId}`, data);
  },
  deleteSale: (saleId) => {
    if (isExplorerMode()) return mockOk();
    return api.delete(`/daybook/sales/${saleId}`);
  },
  addExpense: (id, expense) => {
    if (isExplorerMode()) return mock({ id: nextId(), description: expense.description, amount: expense.amount, createdAt: new Date().toISOString() });
    return api.post(`/daybook/${id}/expenses`, expense);
  },
  deleteExpense: (expenseId) => {
    if (isExplorerMode()) return mockOk();
    return api.delete(`/daybook/expenses/${expenseId}`);
  },
  finalize: (id) => {
    if (isExplorerMode()) return mock({ ...mockDaybookEntry(new Date().toISOString().split('T')[0]), isFinalized: true });
    return api.put(`/daybook/${id}/finalize`);
  },
  repairMonth: (year, month) => {
    if (isExplorerMode()) return mock({ message: 'No repairs needed in demo mode.', corrections: 0 });
    return api.post('/admin/repair-month', null, { params: { year, month } });
  },
};

export const customerService = {
  getAll: (search) => {
    if (isExplorerMode()) {
      const s = search?.toLowerCase();
      const filtered = s ? mockCustomers.filter(c => c.name.toLowerCase().includes(s) || c.phone?.includes(s) || c.vehicleNumber?.toLowerCase().includes(s)) : mockCustomers;
      return mock(filtered);
    }
    return api.get('/customer', { params: { search } });
  },
  getById: (id) => {
    if (isExplorerMode()) return mock(mockCustomers.find(c => c.id === Number(id)) ?? null);
    return api.get(`/customer/${id}`);
  },
  create: (data) => {
    if (isExplorerMode()) return mock({ id: nextId(), ...data, createdAt: new Date().toISOString() });
    return api.post('/customer', data);
  },
  update: (id, data) => {
    if (isExplorerMode()) {
      const existing = mockCustomers.find(c => c.id === Number(id)) ?? {};
      return mock({ ...existing, ...data, id: Number(id) });
    }
    return api.put(`/customer/${id}`, data);
  },
  delete: (id) => {
    if (isExplorerMode()) return mockOk();
    return api.delete(`/customer/${id}`);
  },
};

export const serviceTypeService = {
  getAll: (includeInactive) => {
    if (isExplorerMode()) {
      const list = includeInactive ? mockServiceTypes : mockServiceTypes.filter(s => s.isActive);
      return mock(list);
    }
    return api.get('/servicetype', { params: { includeInactive } });
  },
  create: (data) => {
    if (isExplorerMode()) return mock({ id: nextId(), ...data, isActive: true });
    return api.post('/servicetype', data);
  },
  update: (id, data) => {
    if (isExplorerMode()) {
      const existing = mockServiceTypes.find(s => s.id === Number(id)) ?? {};
      return mock({ ...existing, ...data, id: Number(id) });
    }
    return api.put(`/servicetype/${id}`, data);
  },
  delete: (id) => {
    if (isExplorerMode()) return mockOk();
    return api.delete(`/servicetype/${id}`);
  },
};

export const employeeService = {
  getAll: () => {
    if (isExplorerMode()) return mock(mockEmployees);
    return api.get('/employee');
  },
  getById: (id) => {
    if (isExplorerMode()) return mock(mockEmployees.find(e => e.id === Number(id)) ?? null);
    return api.get(`/employee/${id}`);
  },
  update: (id, data) => {
    if (isExplorerMode()) {
      const existing = mockEmployees.find(e => e.id === Number(id)) ?? {};
      return mock({ ...existing, ...data, id: Number(id) });
    }
    return api.put(`/employee/${id}`, data);
  },
  delete: (id) => {
    if (isExplorerMode()) return mockOk();
    return api.delete(`/employee/${id}`);
  },
};

export const reportService = {
  daily: (date) => {
    if (isExplorerMode()) return mock(mockDailyReport(date ?? new Date().toISOString().split('T')[0]));
    return api.get('/report/daily', { params: { date } });
  },
  monthly: (year, month) => {
    if (isExplorerMode()) return mock(mockMonthlyReport(year, month));
    return api.get('/report/monthly', { params: { year, month } });
  },
  employee: (id, from, to) => {
    if (isExplorerMode()) return mock(mockEmployeeReport(id, from, to));
    return api.get(`/report/employee/${id}`, { params: { from, to } });
  },
  exportCsv: (from, to, employeeId, type, paymentMode) => {
    if (isExplorerMode()) return Promise.resolve({ data: new Blob(['demo,csv,export\n'], { type: 'text/csv' }) });
    return api.get('/report/export', { params: { from, to, employeeId, type, paymentMode }, responseType: 'blob' });
  },
  getInsights: (year, month) => {
    if (isExplorerMode()) return mock(mockInsights(year, month));
    return api.get('/report/insights', { params: { year, month } });
  },
  exportPdf: (year, month, includeAi = false) => {
    if (isExplorerMode()) return Promise.resolve({ data: new Blob([''], { type: 'application/pdf' }) });
    return api.get('/report/export-pdf', { params: { year, month, includeAi }, responseType: 'blob' });
  },
  exportHtml: (year, month, includeAi = false) => {
    if (isExplorerMode()) return Promise.resolve({ data: '<p>Demo HTML export</p>' });
    return api.get('/report/export-html', { params: { year, month, includeAi }, responseType: 'text' });
  },
};

export const adminService = {
  resetData: () => {
    if (isExplorerMode()) return mock({ message: 'All daybook data has been reset successfully.' });
    return api.delete('/admin/reset');
  },
};

export const salaryService = {
  getAll: (year, month) => {
    if (isExplorerMode()) {
      let list = mockSalaryPayments;
      if (year)  list = list.filter(p => new Date(p.date).getFullYear()  === Number(year));
      if (month) list = list.filter(p => new Date(p.date).getMonth() + 1 === Number(month));
      return mock(list);
    }
    return api.get('/salary', { params: { year, month } });
  },
  create: (data) => {
    if (isExplorerMode()) {
      const emp = mockEmployees.find(e => e.id === Number(data.employeeId));
      return mock({ id: nextId(), employeeId: data.employeeId, employeeName: emp?.name ?? 'Unknown', amount: data.amount, date: data.date, notes: data.notes ?? null, createdAt: new Date().toISOString() });
    }
    return api.post('/salary', data);
  },
  update: (id, data) => {
    if (isExplorerMode()) {
      const existing = mockSalaryPayments.find(p => p.id === Number(id)) ?? {};
      return mock({ ...existing, ...data, id: Number(id) });
    }
    return api.put(`/salary/${id}`, data);
  },
  delete: (id) => {
    if (isExplorerMode()) return mockOk();
    return api.delete(`/salary/${id}`);
  },
};

