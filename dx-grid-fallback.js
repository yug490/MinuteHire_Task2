/* Graceful fallback for environments where the DevExtreme CDN is blocked or fails to load.
   DevExtreme's real dxDataGrid is used whenever available. */
(function (window, document) {
  'use strict';
  if (!window.jQuery) return;
  var $ = window.jQuery;
  if ($.fn.dxDataGrid) return;

  $.fn.dxDataGrid = function (options) {
    options = options || {};
    return this.each(function () {
      var host = this;
      var rows = Array.isArray(options.dataSource) ? options.dataSource : [];
      var columns = options.columns || [];
      host.innerHTML = '';
      host.classList.add('dx-grid-fallback');

      if (!rows.length) {
        var empty = document.createElement('div');
        empty.className = 'dx-grid-empty';
        empty.textContent = options.noDataText || 'No records found.';
        host.appendChild(empty);
        return;
      }

      var wrap = document.createElement('div');
      wrap.className = 'dx-grid-fallback-scroll';
      var table = document.createElement('table');
      table.className = 'dx-grid-fallback-table';
      var thead = document.createElement('thead');
      var headRow = document.createElement('tr');
      columns.forEach(function (col) {
        var th = document.createElement('th');
        th.textContent = col.caption || col.dataField || '';
        if (col.width) th.style.width = typeof col.width === 'number' ? col.width + 'px' : col.width;
        headRow.appendChild(th);
      });
      thead.appendChild(headRow);
      table.appendChild(thead);

      var tbody = document.createElement('tbody');
      rows.forEach(function (row, rowIndex) {
        var tr = document.createElement('tr');
        columns.forEach(function (col) {
          var td = document.createElement('td');
          var value = col.dataField ? row[col.dataField] : undefined;
          if (typeof col.cellTemplate === 'function') {
            col.cellTemplate($(td), { value: value, data: row, rowIndex: rowIndex, column: col });
          } else if (value !== undefined && value !== null) {
            if (col.dataType === 'datetime') {
              var date = new Date(value);
              td.textContent = Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString();
            } else {
              td.textContent = String(value);
            }
          } else {
            td.textContent = '';
          }
          tr.appendChild(td);
        });
        tbody.appendChild(tr);
      });
      table.appendChild(tbody);
      wrap.appendChild(table);
      host.appendChild(wrap);
    });
  };
})(window, document);
