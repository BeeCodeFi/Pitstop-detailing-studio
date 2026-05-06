import api from './api';

export const authService = {
  login: (username, password) => api.post('/auth/login', { username, password }),
  register: (data) => api.post('/auth/register', data),
};

export const daybookService = {
  get: (date, employeeId) => api.get('/daybook', { params: { date, employeeId } }),
  updateOpeningBalance: (id, openingBalance) => api.put(`/daybook/${id}/opening-balance`, { openingBalance }),
  addSale: (id, sale) => api.post(`/daybook/${id}/sales`, sale),
  deleteSale: (saleId) => api.delete(`/daybook/sales/${saleId}`),
  addExpense: (id, expense) => api.post(`/daybook/${id}/expenses`, expense),
  deleteExpense: (expenseId) => api.delete(`/daybook/expenses/${expenseId}`),
  finalize: (id) => api.put(`/daybook/${id}/finalize`),
};

export const customerService = {
  getAll: (search) => api.get('/customer', { params: { search } }),
  getById: (id) => api.get(`/customer/${id}`),
  create: (data) => api.post('/customer', data),
  update: (id, data) => api.put(`/customer/${id}`, data),
  delete: (id) => api.delete(`/customer/${id}`),
};

export const serviceTypeService = {
  getAll: (includeInactive) => api.get('/servicetype', { params: { includeInactive } }),
  create: (data) => api.post('/servicetype', data),
  update: (id, data) => api.put(`/servicetype/${id}`, data),
  delete: (id) => api.delete(`/servicetype/${id}`),
};

export const employeeService = {
  getAll: () => api.get('/employee'),
  getById: (id) => api.get(`/employee/${id}`),
  update: (id, data) => api.put(`/employee/${id}`, data),
  delete: (id) => api.delete(`/employee/${id}`),
};

export const reportService = {
  daily: (date) => api.get('/report/daily', { params: { date } }),
  monthly: (year, month) => api.get('/report/monthly', { params: { year, month } }),
  employee: (id, from, to) => api.get(`/report/employee/${id}`, { params: { from, to } }),
  exportCsv: (from, to) => api.get('/report/export', { params: { from, to }, responseType: 'blob' }),
};

export const adminService = {
  resetData: () => api.delete('/admin/reset'),
};

export const salaryService = {
  getAll: (year, month) => api.get('/salary', { params: { year, month } }),
  create: (data) => api.post('/salary', data),
  update: (id, data) => api.put(`/salary/${id}`, data),
  delete: (id) => api.delete(`/salary/${id}`),
};
