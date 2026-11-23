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

  var toolbar = document.querySelector('.editor-toolbar');
  var status = document.getElementById('editor-status');
  var messages = {
    youtubePrompt: toolbar && toolbar.getAttribute('data-msg-youtube-prompt'),
    youtubeInvalid: toolbar && toolbar.getAttribute('data-msg-youtube-invalid'),
    uploading: toolbar && toolbar.getAttribute('data-msg-uploading'),
    uploadFailed: toolbar && toolbar.getAttribute('data-msg-upload-failed'),
    tooLarge: toolbar && toolbar.getAttribute('data-msg-too-large')
  };
  function msg(key, fallback) { return messages[key] || fallback; }

  // Nhận watch?v=, youtu.be/, /embed/, /shorts/, /live/ (kèm t=90 / t=1m30s) hoặc chính ID 11 ký tự.
  function parseYouTube(input) {
    input = (input || '').trim();
    var ID = /^[A-Za-z0-9_-]{11}$/;
    if (ID.test(input)) return { id: input, start: 0 };
    var u;
    try { u = new URL(/^https?:\/\//i.test(input) ? input : 'https://' + input); } catch (e) { return null; }
    var host = u.hostname.replace(/^(www|m|music)\./, '');
    var id = null, m;
    if (host === 'youtu.be') id = u.pathname.split('/')[1];
    else if (host === 'youtube.com' || host === 'youtube-nocookie.com') {
      if (u.pathname === '/watch') id = u.searchParams.get('v');
      else if ((m = u.pathname.match(/^\/(?:embed|shorts|live|v)\/([A-Za-z0-9_-]{11})/))) id = m[1];
    }
    if (!id || !ID.test(id)) return null;

    var t = u.searchParams.get('t') || u.searchParams.get('start') || '';
    var start = 0;
    if (/^\d+$/.test(t)) start = parseInt(t, 10);
    else if ((m = t.match(/^(?:(\d+)h)?(?:(\d+)m)?(?:(\d+)s)?$/)) && t) start = (+m[1] || 0) * 3600 + (+m[2] || 0) * 60 + (+m[3] || 0);
    return { id: id, start: start };
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
    youtube: function () {
      var raw = window.prompt(msg('youtubePrompt', 'Paste a YouTube link:'));
      if (!raw) return;
      var v = parseYouTube(raw);
      if (!v) { window.alert(msg('youtubeInvalid', 'Not a valid YouTube link.')); return; }
      var src = 'https://www.youtube-nocookie.com/embed/' + v.id + (v.start ? '?start=' + v.start : '');
      insert('\n<div class="video-embed"><iframe src="' + src + '" title="YouTube video" loading="lazy" ' +
        'allow="accelerometer; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share" ' +
        'referrerpolicy="strict-origin-when-cross-origin" allowfullscreen></iframe></div>\n');
    },
    video: function () { document.getElementById('video-file').click(); },
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

  // ---- Upload video (XHR để hiện % tiến độ; video có thể nặng hàng trăm MB) ----
  var videoInput = document.getElementById('video-file');
  if (videoInput) {
    videoInput.addEventListener('change', function () {
      if (!videoInput.files.length) return;
      var data = new FormData();
      data.append('file', videoInput.files[0]);
      data.append('__RequestVerificationToken', token);

      var buttons = document.querySelectorAll('.editor-toolbar button');
      function busy(on) { buttons.forEach(function (b) { b.disabled = on; }); }
      function done(text) { busy(false); status.textContent = text || ''; videoInput.value = ''; }

      var xhr = new XMLHttpRequest();
      xhr.open('POST', '/admin/media/uploadvideo');
      xhr.withCredentials = true;
      xhr.upload.onprogress = function (e) {
        if (e.lengthComputable) status.textContent = msg('uploading', 'Uploading video…') + ' ' + Math.round(e.loaded / e.total * 100) + '%';
      };
      xhr.onerror = function () { done(); window.alert(msg('uploadFailed', 'Video upload failed.')); };
      xhr.onload = function () {
        var body = null;
        try { body = JSON.parse(xhr.responseText); } catch (e) { /* trả về HTML (vd. 413) */ }
        if (xhr.status >= 200 && xhr.status < 300 && body && body.url) {
          insert('\n<video controls preload="metadata" playsinline src="' + body.url + '"></video>\n');
          done();
        } else {
          done();
          window.alert(xhr.status === 413 ? msg('tooLarge', 'The video is too large for the server limit.')
            : (body && body.error) || msg('uploadFailed', 'Video upload failed.'));
        }
      };
      busy(true);
      status.textContent = msg('uploading', 'Uploading video…') + ' 0%';
      xhr.send(data);
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
