(function () {
  var csrf = document.querySelector('meta[name="csrf-token"]');
  var token = csrf ? csrf.content : '';

  // ---- Xác nhận thao tác nguy hiểm: <form data-confirm="..."> ----
  document.addEventListener('submit', function (e) {
    var msg = e.target.getAttribute && e.target.getAttribute('data-confirm');
    if (msg && !window.confirm(msg)) e.preventDefault();
  });

  // ---- Trình soạn thảo HTML đơn giản ----
  var editor = document.getElementById('editor');
  if (!editor) return;

  function wrap(before, after) {
    var start = editor.selectionStart, end = editor.selectionEnd;
    var selected = editor.value.substring(start, end);
    editor.setRangeText(before + selected + after, start, end, 'end');
    editor.focus();
  }

  function insert(text) {
    var start = editor.selectionStart;
    editor.setRangeText(text, start, editor.selectionEnd, 'end');
    editor.focus();
  }

  var actions = {
    bold: function () { wrap('<strong>', '</strong>'); },
    italic: function () { wrap('<em>', '</em>'); },
    h2: function () { wrap('<h2>', '</h2>'); },
    h3: function () { wrap('<h3>', '</h3>'); },
    quote: function () { wrap('<blockquote>', '</blockquote>'); },
    code: function () { wrap('<pre><code>', '</code></pre>'); },
    ul: function () { wrap('<ul>\n  <li>', '</li>\n</ul>'); },
    hr: function () { insert('\n<hr />\n'); },
    link: function () {
      var url = window.prompt('Link URL (https://...)');
      if (url && /^(https?:\/\/|\/|#|mailto:)/i.test(url)) wrap('<a href="' + url.replace(/"/g, '&quot;') + '">', '</a>');
    },
    image: function () { document.getElementById('image-file').click(); },
    preview: function () {
      var frame = document.getElementById('preview');
      var showing = !frame.hidden;
      frame.hidden = showing;
      editor.hidden = !showing;
      // iframe sandbox: không chạy script trong bản xem trước.
      if (!showing) frame.srcdoc = '<base target="_blank"><body style="font-family:sans-serif;line-height:1.6;padding:1rem">' + editor.value;
    }
  };

  document.querySelectorAll('[data-editor-action]').forEach(function (btn) {
    btn.addEventListener('click', function () { actions[btn.getAttribute('data-editor-action')](); });
  });

  var fileInput = document.getElementById('image-file');
  if (fileInput) {
    fileInput.addEventListener('change', function () {
      if (!fileInput.files.length) return;
      var data = new FormData();
      data.append('file', fileInput.files[0]);
      data.append('__RequestVerificationToken', token);
      fetch('/admin/media/uploadimage', { method: 'POST', body: data, credentials: 'same-origin' })
        .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, body: j }; }); })
        .then(function (res) {
          if (!res.ok) { window.alert(res.body.error || 'Upload failed'); return; }
          insert('<img src="' + res.body.url + '" alt="" />');
        })
        .catch(function () { window.alert('Upload failed'); })
        .finally(function () { fileInput.value = ''; });
    });
  }

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
