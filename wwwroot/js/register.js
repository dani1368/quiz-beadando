$(function () {
  $('#signupForm').submit(function (e) {
    e.preventDefault();
    const email = $('#email').val();
    const password = $('#password').val();

    if (!email || !password) {
      $('#message').html('<div class="alert alert-danger">Tölts ki minden mezőt!</div>');
      return;
    }

    fetch('/api/session/register', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({ email, password })
    })
      .then(async res => {
        if (res.ok) {
          $('#message').html('<div class="alert alert-success">Sikeres regisztráció!</div>');
          setTimeout(() => window.location.href = '/index.html', 1000);
        } else {
          const data = await res.json();
          let msg = 'Ismeretlen hiba történt.';
          if (data.reason === 'exists') msg = 'Ez az email már regisztrálva van.';
          else if (data.reason === 'empty') msg = 'Tölts ki minden mezőt!';
          $('#message').html(`<div class="alert alert-danger">${msg}</div>`);
        }
      })
      .catch(() => {
        $('#message').html('<div class="alert alert-danger">A szerver nem válaszol.</div>');
      });
  });
});