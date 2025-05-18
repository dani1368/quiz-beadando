$(function () {
  const list = $('#quizList');
  list.html('<div class="text-muted">Betöltés...</div>');

  fetch('/api/quiz/history', { credentials: 'include' })
    .then(res => res.json())
    .then(data => {
      if (!data.success || !data.quizzes || data.quizzes.length === 0) {
        list.html('<div class="alert alert-warning">Még nem töltöttél ki egyetlen kvízt sem.</div>');
        return;
      }

      const table = $('<table class="table table-bordered table-striped"></table>');
      table.append('<thead><tr><th>Dátum</th><th>Pontszám</th></tr></thead>');
      const tbody = $('<tbody></tbody>');

      data.quizzes.forEach(q => {
        const date = new Date(q.timestamp * 1000).toLocaleString();
        tbody.append(`<tr><td>${date}</td><td>${q.score}</td></tr>`);
      });

      table.append(tbody);
      list.html(table);
    })
    .catch(() => {
      list.html('<div class="alert alert-danger">Nem sikerült lekérni a korábbi kvízeket.</div>');
    });
});
