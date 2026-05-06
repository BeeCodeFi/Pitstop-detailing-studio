export default function StatCard({ title, value, subtitle, icon: Icon, color = 'primary' }) {
  const colors = {
    primary: 'bg-primary/10 text-primary',
    success: 'bg-success/10 text-success',
    warning: 'bg-warning/10 text-warning',
    danger: 'bg-danger/10 text-danger',
    accent: 'bg-accent/10 text-accent',
  };

  const borderColors = {
    primary: 'hover:border-primary/30',
    success: 'hover:border-success/30',
    warning: 'hover:border-warning/30',
    danger: 'hover:border-danger/30',
    accent: 'hover:border-accent/30',
  };

  return (
    <div className={`bg-white rounded-xl border border-gray-200 p-5 hover:shadow-lg transition-all duration-200 ${borderColors[color]} group`}>
      <div className="flex items-center justify-between mb-3">
        <p className="text-xs font-medium text-gray-500 uppercase tracking-wide">{title}</p>
        {Icon && (
          <div className={`p-2 rounded-lg ${colors[color]} transition-transform duration-200 group-hover:scale-110`}>
            <Icon className="w-4 h-4" />
          </div>
        )}
      </div>
      <p className="text-2xl font-bold text-gray-900 tracking-tight">₹{Number(value).toLocaleString('en-IN')}</p>
      {subtitle && <p className="text-xs text-gray-400 mt-1.5">{subtitle}</p>}
    </div>
  );
}
