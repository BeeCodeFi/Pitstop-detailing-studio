import { Inbox } from 'lucide-react';

export default function DataTable({ columns, data, emptyMessage = 'No data found' }) {
  if (!data || data.length === 0) {
    return (
      <div className="bg-white rounded-xl border border-gray-200 p-12 text-center">
        <Inbox className="w-12 h-12 text-gray-300 mx-auto mb-3" />
        <p className="text-gray-400 text-sm">{emptyMessage}</p>
      </div>
    );
  }

  return (
    <div className="bg-white rounded-xl border border-gray-200 overflow-hidden shadow-sm">
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="bg-gray-50/80 border-b border-gray-200">
              {columns.map((col, i) => (
                <th key={i} className="px-4 py-3.5 text-left text-xs font-semibold text-gray-500 uppercase tracking-wider">
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {data.map((row, rowIdx) => (
              <tr key={row.id || rowIdx} className="hover:bg-primary/[0.02] transition-colors">
                {columns.map((col, colIdx) => (
                  <td key={colIdx} className="px-4 py-3.5 text-gray-700 whitespace-nowrap">
                    {col.render ? col.render(row) : row[col.accessor]}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {data.length > 10 && (
        <div className="px-4 py-2.5 bg-gray-50/50 border-t border-gray-100 text-xs text-gray-400 text-right">
          Showing {data.length} records
        </div>
      )}
    </div>
  );
}
