function escapeHtml(text) 
{
  return text
    .replace(/&/g, "&amp;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&#039;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;");
}

$(function () {
  const container = $('#quizContainer');

  // 2. Beküldés
  $('#quizForm').submit(function (e) {
    e.preventDefault();
    const formData = $(this).serialize();

    fetch('/api/quiz/start')
        .then(res => {
        if (res.status === 401) throw new Error("Nem vagy bejelentkezve.");
        if (!res.ok) throw new Error("Nem sikerült betölteni a kérdéseket.");
        return res.json();
    })
    .then(data => {
    // ... renderelés mehet
          data.questions.forEach((q, i) => {
        const questionId = q.id;

        // Képblokk előállítása
        let imageHTML = '';
        if (q.imageUrls && q.imageUrls.length > 0) {
          imageHTML = '<div class="d-flex flex-wrap gap-2 mb-2">';
          q.imageUrls.forEach(url => {
            imageHTML += `<img src="${url}" class="img-fluid rounded border" style="max-width: 200px; max-height: 150px;" alt="Kérdés képe">`;
          });
          imageHTML += '</div>';
        }

        const questionHTML = $(`
          <div class="mb-5 border rounded p-3 shadow-sm bg-light">
            <h5>${i + 1}. ${q.text}</h5>
            ${imageHTML}
            <div class="form-check-group mt-2" id="question-${questionId}"></div>
          </div>
        `);

        q.answers.forEach(answer => {
          const safeText = escapeHtml(answer.text);
          const radio = `
            <div class="form-check">
              <input class="form-check-input" type="radio" name="q${questionId}" id="a${answer.id}" value="${answer.id}" required>
              <label class="form-check-label" for="a${answer.id}">${safeText}</label>
            </div>
          `;
          questionHTML.find(`#question-${questionId}`).append(radio);
        });

        container.append(questionHTML);
      });
    })
    .catch(err => {
        $('#quizContainer').html(`
        <div class="alert alert-warning">
            ${err.message}<br>
            <a href="/login.html" class="btn btn-sm btn-primary mt-2">Bejelentkezés</a>
        </div>
        `);
    });
  });
});