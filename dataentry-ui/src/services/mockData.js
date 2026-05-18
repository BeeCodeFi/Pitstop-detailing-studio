// ─── Demo data for Explorer role ────────────────────────────────────────────
// All data here is fictional and used solely for the Explorer demo experience.
// Nothing in this file touches the real database.

const ts = (offsetMinutes = 0) => {
  const d = new Date();
  d.setMinutes(d.getMinutes() - offsetMinutes);
  return d.toISOString();
};

// ── Customers ────────────────────────────────────────────────────────────────
export const mockCustomers = [
  { id: 101, name: 'Rahul Sharma',  phone: '9876543210', vehicleNumber: 'MH12AB1234', vehicleType: 'SUV',      notes: 'Regular customer',         createdAt: '2026-04-10T09:00:00Z' },
  { id: 102, name: 'Priya Mehta',   phone: '9123456780', vehicleNumber: 'DL3CDE5678', vehicleType: 'Sedan',    notes: null,                       createdAt: '2026-04-22T11:30:00Z' },
  { id: 103, name: 'Vikram Patel',  phone: '9988776655', vehicleNumber: 'GJ5FGH9012', vehicleType: 'Hatchback',notes: 'Prefers ceramic coating',   createdAt: '2026-05-01T10:15:00Z' },
  { id: 104, name: 'Sunita Joshi',  phone: '9012345678', vehicleNumber: 'KA01MN3456', vehicleType: 'Sedan',    notes: null,                       createdAt: '2026-05-05T14:00:00Z' },
  { id: 105, name: 'Arjun Nair',    phone: '8765432109', vehicleNumber: 'TN22PQ7890', vehicleType: 'SUV',      notes: 'Fleet vehicle',            createdAt: '2026-05-10T09:45:00Z' },
  { id: 106, name: 'Meena Iyer',    phone: '9654321087', vehicleNumber: 'KL07RS1122', vehicleType: 'Hatchback',notes: null,                       createdAt: '2026-05-12T16:20:00Z' },
];

// ── Service types (mirror seeded DB values) ───────────────────────────────────
export const mockServiceTypes = [
  { id: 1,  name: 'Exterior Wash',            defaultPrice: 500,  isActive: true  },
  { id: 2,  name: 'Interior Cleaning',         defaultPrice: 800,  isActive: true  },
  { id: 3,  name: 'Full Detailing',            defaultPrice: 2500, isActive: true  },
  { id: 4,  name: 'Polish & Wax',              defaultPrice: 1500, isActive: true  },
  { id: 5,  name: 'Ceramic Coating',           defaultPrice: 8000, isActive: true  },
  { id: 6,  name: 'Engine Bay Cleaning',       defaultPrice: 1000, isActive: true  },
  { id: 7,  name: 'Headlight Restoration',     defaultPrice: 600,  isActive: true  },
  { id: 8,  name: 'Seat/Upholstery Cleaning',  defaultPrice: 1200, isActive: true  },
  { id: 9,  name: 'AC Vent Sanitization',      defaultPrice: 400,  isActive: true  },
  { id: 10, name: 'Tyre Dressing',             defaultPrice: 300,  isActive: false },
];

// ── Employees ────────────────────────────────────────────────────────────────
export const mockEmployees = [
  { id: 1, name: 'Demo Admin',   username: 'admin_demo',  role: 'Admin',    phone: '9800000001', isActive: true, createdAt: '2026-01-01T00:00:00Z' },
  { id: 2, name: 'Ravi Kumar',   username: 'ravi_demo',   role: 'Employee', phone: '9800000002', isActive: true, createdAt: '2026-01-15T00:00:00Z' },
  { id: 3, name: 'Sneha Verma',  username: 'sneha_demo',  role: 'Employee', phone: '9800000003', isActive: true, createdAt: '2026-02-01T00:00:00Z' },
];

// ── Daybook helpers ──────────────────────────────────────────────────────────
const demoDaybookSales = [
  { id: 9001, customerId: 101, customerName: 'Rahul Sharma', serviceTypeId: 1,  serviceTypeName: 'Exterior Wash',       vehicleNumber: 'MH12AB1234', vehicleType: 'SUV',       amount: 500,  paymentMode: 'Cash', notes: null,               createdAt: ts(300), vehicleVisitCount: 4 },
  { id: 9002, customerId: 102, customerName: 'Priya Mehta',  serviceTypeId: 3,  serviceTypeName: 'Full Detailing',      vehicleNumber: 'DL3CDE5678', vehicleType: 'Sedan',     amount: 2500, paymentMode: 'Card', notes: 'Monthly package',  createdAt: ts(240), vehicleVisitCount: 2 },
  { id: 9003, customerId: null,customerName: null,            serviceTypeId: 2,  serviceTypeName: 'Interior Cleaning',   vehicleNumber: 'KA09ZX4321', vehicleType: 'Hatchback', amount: 800,  paymentMode: 'UPI',  notes: null,               createdAt: ts(180), vehicleVisitCount: 1 },
  { id: 9004, customerId: 103, customerName: 'Vikram Patel', serviceTypeId: 9,  serviceTypeName: 'AC Vent Sanitization', vehicleNumber: 'GJ5FGH9012', vehicleType: 'Hatchback', amount: 400,  paymentMode: 'Cash', notes: null,               createdAt: ts(120), vehicleVisitCount: 3 },
  { id: 9005, customerId: null,customerName: null,            serviceTypeId: 10, serviceTypeName: 'Tyre Dressing',       vehicleNumber: 'MH01TR8899', vehicleType: 'SUV',       amount: 300,  paymentMode: 'Cash', notes: null,               createdAt: ts(60),  vehicleVisitCount: 1 },
];

const demoDaybookExpenses = [
  { id: 8001, description: 'Cleaning supplies', amount: 250, createdAt: ts(270) },
  { id: 8002, description: 'Electricity bill',  amount: 100, createdAt: ts(200) },
];

// Total: sales=4500, cash=1200, card=2500, upi=800, pending=0, expenses=350
export const mockDaybookEntry = (date) => ({
  id: 999,
  employeeId: 1,
  employeeName: 'Demo Admin',
  date,
  openingBalance:       5000,
  totalSales:           4500,
  totalCashCollected:   1200,
  totalCardCollected:   2500,
  totalUpiCollected:    800,
  totalPendingCollected: 0,
  totalExpenses:        350,
  closingBalance:       9150,
  notes: null,
  isFinalized: false,
  sales: demoDaybookSales,
  expenses: demoDaybookExpenses,
});

// Additional sales by Ravi (employee 2) for the all-sales combined view
const raviSales = [
  { id: 9006, employeeId: 2, employeeName: 'Ravi Kumar', customerId: null,  customerName: null,            serviceTypeId: 1, serviceTypeName: 'Exterior Wash', vehicleNumber: 'MH15GH2233', vehicleType: 'Hatchback', amount: 500,  paymentMode: 'Cash', notes: null, createdAt: ts(150), vehicleVisitCount: 1 },
  { id: 9007, employeeId: 2, employeeName: 'Ravi Kumar', customerId: 104,   customerName: 'Sunita Joshi',  serviceTypeId: 4, serviceTypeName: 'Polish & Wax',  vehicleNumber: 'KA01MN3456', vehicleType: 'Sedan',     amount: 1500, paymentMode: 'UPI', notes: null, createdAt: ts(90),  vehicleVisitCount: 2 },
];

export const mockAllSales = (date) => ({
  date,
  sales: [
    ...demoDaybookSales.map(s => ({ ...s, employeeId: 1, employeeName: 'Demo Admin' })),
    ...raviSales,
  ],
  totalSales:    6500,
  totalCash:     1700,
  totalCard:     2500,
  totalUpi:      2300,
  totalPending:  0,
  transactionCount: 7,
  combinedOpeningBalance: 5000,
  combinedExpenses:       350,
  combinedClosingBalance: 11150,
  isFinalized: false,
});

// ── Reports ──────────────────────────────────────────────────────────────────
const dailyTotals = [
  { date: '2026-05-01', totalSales: 8500,  totalCash: 4000, totalExpenses: 500, transactionCount: 12 },
  { date: '2026-05-02', totalSales: 6200,  totalCash: 3000, totalExpenses: 200, transactionCount: 9  },
  { date: '2026-05-03', totalSales: 7800,  totalCash: 3500, totalExpenses: 300, transactionCount: 11 },
  { date: '2026-05-05', totalSales: 9100,  totalCash: 4500, totalExpenses: 400, transactionCount: 13 },
  { date: '2026-05-06', totalSales: 5500,  totalCash: 2500, totalExpenses: 150, transactionCount: 8  },
  { date: '2026-05-07', totalSales: 8200,  totalCash: 4000, totalExpenses: 350, transactionCount: 12 },
  { date: '2026-05-08', totalSales: 7300,  totalCash: 3200, totalExpenses: 250, transactionCount: 10 },
  { date: '2026-05-09', totalSales: 6800,  totalCash: 3000, totalExpenses: 200, transactionCount: 9  },
  { date: '2026-05-10', totalSales: 9500,  totalCash: 4800, totalExpenses: 450, transactionCount: 14 },
  { date: '2026-05-12', totalSales: 7100,  totalCash: 3200, totalExpenses: 300, transactionCount: 10 },
  { date: '2026-05-13', totalSales: 8400,  totalCash: 4100, totalExpenses: 350, transactionCount: 12 },
  { date: '2026-05-14', totalSales: 6900,  totalCash: 3300, totalExpenses: 250, transactionCount: 10 },
  { date: '2026-05-15', totalSales: 7600,  totalCash: 3600, totalExpenses: 300, transactionCount: 11 },
  { date: '2026-05-17', totalSales: 8800,  totalCash: 4200, totalExpenses: 380, transactionCount: 13 },
  { date: '2026-05-18', totalSales: 6500,  totalCash: 1700, totalExpenses: 350, transactionCount: 7  },
];

export const mockMonthlyReport = (year, month) => ({
  year,
  month,
  dailyTotals,
  grandTotalSales:    114200,
  grandTotalCash:     52600,
  grandTotalExpenses: 4730,
  grandTotalSalaries: 34000,
  grandTotalPending:  3200,
  netIncome:          75470,
});

export const mockDailyReport = (date) => ({
  date,
  employees: [
    { employeeId: 1, employeeName: 'Demo Admin',  openingBalance: 5000, totalSales: 4500, totalCash: 1200, totalCard: 2500, totalUpi: 800,  totalPending: 0, totalExpenses: 350, closingBalance: 9150 },
    { employeeId: 2, employeeName: 'Ravi Kumar',  openingBalance: 0,    totalSales: 2000, totalCash: 500,  totalCard: 0,    totalUpi: 1500, totalPending: 0, totalExpenses: 0,   closingBalance: 2000 },
  ],
  grandTotalSales:    6500,
  grandTotalCash:     1700,
  grandTotalCard:     2500,
  grandTotalUpi:      2300,
  grandTotalPending:  0,
  grandTotalExpenses: 350,
});

export const mockEmployeeReport = (employeeId, from, to) => ({
  employeeId,
  employeeName: mockEmployees.find(e => e.id === Number(employeeId))?.name ?? 'Demo Employee',
  from,
  to,
  entries: dailyTotals.slice(0, 8).map(d => ({
    date:            d.date,
    openingBalance:  5000,
    totalSales:      d.totalSales,
    totalCash:       d.totalCash,
    totalExpenses:   d.totalExpenses,
    closingBalance:  5000 + d.totalSales - d.totalExpenses,
    transactionCount: d.transactionCount,
  })),
  totalSales:    58600,
  totalCash:     28000,
  totalExpenses: 2350,
});

// ── Insights ─────────────────────────────────────────────────────────────────
export const mockInsights = (year, month) => ({
  year,
  month,
  totalSales:           114200,
  totalExpenses:        4730,
  netIncome:            75470,
  revenueGrowthPercent: 12.3,
  averageDailySales:    7613,
  pendingAmount:        3200,
  totalTransactions:    151,
  topService:           'Full Detailing',
  topPaymentMode:       'Cash',
  bestDay:              'Saturday',
  worstDay:             'Tuesday',
  serviceBreakdown: [
    { serviceName: 'Full Detailing',           totalRevenue: 37500, transactionCount: 15, percentage: 32.8 },
    { serviceName: 'Polish & Wax',             totalRevenue: 24000, transactionCount: 16, percentage: 21.0 },
    { serviceName: 'Exterior Wash',            totalRevenue: 18500, transactionCount: 37, percentage: 16.2 },
    { serviceName: 'Interior Cleaning',        totalRevenue: 14400, transactionCount: 18, percentage: 12.6 },
    { serviceName: 'Ceramic Coating',          totalRevenue: 12000, transactionCount:  2, percentage: 10.5 },
    { serviceName: 'Seat/Upholstery Cleaning', totalRevenue:  4800, transactionCount:  4, percentage:  4.2 },
    { serviceName: 'Engine Bay Cleaning',      totalRevenue:  2000, transactionCount:  2, percentage:  1.7 },
    { serviceName: 'Headlight Restoration',    totalRevenue:  1000, transactionCount:  2, percentage:  0.9 },
  ],
  paymentModeBreakdown: [
    { paymentMode: 'Cash',    amount: 52600, count: 90, percentage: 46.1 },
    { paymentMode: 'Card',    amount: 32400, count: 32, percentage: 28.4 },
    { paymentMode: 'UPI',     amount: 26000, count: 25, percentage: 22.8 },
    { paymentMode: 'Pending', amount:  3200, count:  4, percentage:  2.8 },
  ],
  dayOfWeekBreakdown: [
    { dayName: 'Monday',    averageSales: 6800,  transactionCount: 18 },
    { dayName: 'Tuesday',   averageSales: 5200,  transactionCount: 14 },
    { dayName: 'Wednesday', averageSales: 7100,  transactionCount: 19 },
    { dayName: 'Thursday',  averageSales: 7400,  transactionCount: 20 },
    { dayName: 'Friday',    averageSales: 8200,  transactionCount: 22 },
    { dayName: 'Saturday',  averageSales: 11500, transactionCount: 30 },
    { dayName: 'Sunday',    averageSales: 9800,  transactionCount: 28 },
  ],
  aiSummary: 'Demo month showing strong revenue growth of 12.3% versus the previous month. Full Detailing remains the highest-revenue service. Saturday and Sunday together account for over 40% of weekly transactions.',
  aiInsights: [
    { title: 'Revenue Growth',    description: 'Total sales up 12.3% compared to last month.', type: 'positive' },
    { title: 'Top Service',       description: 'Full Detailing contributes 32.8% of total revenue.', type: 'positive' },
    { title: 'Pending Payments',  description: '₹3,200 in pending payments — follow up with customers.', type: 'negative' },
    { title: 'Weekend Peak',      description: 'Saturday averages ₹11,500/day — consider extra staff.', type: 'neutral'  },
  ],
  aiRecommendations: [
    'Introduce a loyalty card programme for regular customers.',
    'Upsell interior cleaning to Exterior Wash customers.',
    'Consider a Tuesday discount offer to boost the slowest day.',
  ],
  aiAlerts: [
    '4 transactions still in Pending payment status totalling ₹3,200.',
  ],
});

// ── Salary payments ───────────────────────────────────────────────────────────
export const mockSalaryPayments = [
  { id: 701, employeeId: 2, employeeName: 'Ravi Kumar',  amount: 18000, date: '2026-04-30', notes: 'April salary', createdAt: '2026-04-30T18:00:00Z' },
  { id: 702, employeeId: 3, employeeName: 'Sneha Verma', amount: 16000, date: '2026-04-30', notes: 'April salary', createdAt: '2026-04-30T18:30:00Z' },
  { id: 703, employeeId: 2, employeeName: 'Ravi Kumar',  amount: 18000, date: '2026-03-31', notes: 'March salary', createdAt: '2026-03-31T17:00:00Z' },
  { id: 704, employeeId: 3, employeeName: 'Sneha Verma', amount: 16000, date: '2026-03-31', notes: 'March salary', createdAt: '2026-03-31T17:30:00Z' },
];
