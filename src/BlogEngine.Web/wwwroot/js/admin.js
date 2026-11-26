(function () {
  // ---- Xác nhận thao tác nguy hiểm: <form data-confirm="..."> ----
  document.addEventListener('submit', function (e) {
    var msg = e.target.getAttribute && e.target.getAttribute('data-confirm');
    if (msg && !window.confirm(msg)) e.preventDefault();
  });

  // Tự điền slug gợi ý từ tiêu đề nếu slug đang trống (chỉ khi tạo mới).
  var title = document.getElementById('Title');
  var slug = document.getElementById('Slug');
  if (title && slug && !slug.value) {
    title.addEventListener('blur', function () {
      if (slug.value) return;
      slug.placeholder = title.value.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '')
        .replace(/đ/g, 'd').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
    });
  }
})();

// Nút "Copy link" trong trang Media: <button data-copy="/uploads/..." data-copied="Đã chép!">
document.addEventListener('click', function (e) {
  var btn = e.target.closest && e.target.closest('[data-copy]');
  if (!btn) return;
  var url = location.origin + btn.getAttribute('data-copy');
  var done = function () {
    var old = btn.textContent;
    btn.textContent = btn.getAttribute('data-copied') || 'Copied!';
    setTimeout(function () { btn.textContent = old; }, 1500);
  };
  if (navigator.clipboard && window.isSecureContext) navigator.clipboard.writeText(url).then(done);
  else { var t = document.createElement('input'); t.value = url; document.body.appendChild(t); t.select(); document.execCommand('copy'); t.remove(); done(); }
});
