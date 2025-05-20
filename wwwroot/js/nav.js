$.get("/api/session/check", (data) => {
  const show = (sel) => $(sel).removeClass("d-none");
  const hide = (sel) => $(sel).addClass("d-none");

  if (data.loggedIn) {
    hide("#loginNav, #signupNav");
    show("#accountNav, #logoutNav");
  } else {
    show("#loginNav, #signupNav");
    hide("#accountNav, #logoutNav");
  }
});

$("#logoutNav").on("click", (e) => {
  e.preventDefault();
  $.post("/api/session/logout", () => window.location.href = "/login.html");
});
