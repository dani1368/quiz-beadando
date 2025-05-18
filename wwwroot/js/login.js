$(function () {
  $('#loginForm').submit(function (e) {
    e.preventDefault();
    const email = $('#email').val();
    const password = $('#password').val();

    if (!email || !password) {
      $('#message').html('<div class="alert alert-danger">Tölts ki minden mezőt!</div>');
      return;
    }

    fetch('/api/session/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ email, password })
    })
      .then(async res => {
        if (res.ok) {
          $('#message').html('<div class="alert alert-success">Sikeres bejelentkezés!</div>');
          setTimeout(() => window.location.href = '/index.html', 1000);
        } else {
          const data = await res.json();
          let msg = 'Ismeretlen hiba.';
          if (data.reason === 'email') msg = 'Ez az email nem létezik.';
          else if (data.reason === 'password') msg = 'Hibás jelszó.';
          $('#message').html(`<div class="alert alert-danger">${msg}</div>`);
        }
      })
      .catch(() => {
        $('#message').html('<div class="alert alert-danger">A szerver nem válaszol.</div>');
      });
  });
});