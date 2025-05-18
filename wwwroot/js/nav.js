fetch("/api/session/check")
  .then(res => res.json())
  .then(data => {
    const loggedIn = data.loggedIn;
    console.log("Session check:", loggedIn);

    if (loggedIn) {
      $("#loginNav, #signupNav").addClass("d-none");
      $("#accountNav, #logoutNav").removeClass("d-none");
    } else {
      $("#loginNav, #signupNav").removeClass("d-none");
      $("#accountNav, #logoutNav").addClass("d-none");
    }
  });

$("#logoutNav").on("click", function (e) {
  e.preventDefault();
  fetch("/api/session/logout", { method: "POST" })
    .then(() => window.location.href = "/login.html");
});