$(function () {
  const container = $('#quizContainer');

  // 1. Lekérjük a kérdéseket a backendről
  fetch('/api/quiz/start')
    .then(res => {
      if (!res.ok) throw new Error("Nem sikerült betölteni a kérdéseket.");
      return res.json();
    })
    .then(data => {
      data.questions.forEach((q, i) => {
        const questionId = q.id;
        const questionHTML = $(`
          <div class="mb-4">
            <h5>${i + 1}. ${q.text}</h5>
            ${q.imageUrl ? `<img src="${q.imageUrl}" class="img-fluid mb-2" alt="Kérdés képe">` : ""}
            <div class="form-check-group" id="question-${questionId}"></div>
          </div>
        `);

        q.answers.forEach(answer => {
          const radio = `
            <div class="form-check">
              <input class="form-check-input" type="radio" name="q${questionId}" id="a${answer.id}" value="${answer.id}" required>
              <label class="form-check-label" for="a${answer.id}">${answer.text}</label>
            </div>
          `;
          questionHTML.find(`#question-${questionId}`).append(radio);
        });

        container.append(questionHTML);
      });
    })
    .catch(err => {
      container.html(`<div class="alert alert-danger">${err.message}</div>`);
    });

  // 2. Beküldés
  $('#quizForm').submit(function (e) {
    e.preventDefault();
    const formData = $(this).serialize();

    fetch('/api/quiz/submit', {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: formData
    })
      .then(res => res.json())
      .then(result => {
        if (result.success === false) {
          $('#quizMessage').html(`<div class="alert alert-danger">${result.message}</div>`);
        } else {
          $('#quizMessage').html(`<div class="alert alert-info">Pontszámod: ${result.score} / ${result.total}</div>`);
          $('#quizForm button[type=submit]').prop('disabled', true);
        }
      })
      .catch(() => {
        $('#quizMessage').html('<div class="alert alert-danger">Nem sikerült beküldeni a kvízt.</div>');
      });
  });
});