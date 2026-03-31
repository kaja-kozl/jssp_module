using Terminal.Gui;

// Defines a top-level window with border and title
public class ChooseFileWindow : Window {
   // public static string Username { get; internal set; }
    //public TextField usernameText;

    public ListView filesView { get; internal set; }

    public string?[] fetchFiles()
    {
        return Directory.GetFiles ("bin\\Debug\\net10.0", "*.csv").Select (Path.GetFileName).ToArray ();
    }

    public ChooseFileWindow ()
    {
        Title = "JSSP Optimizer";

        string?[] fileList = fetchFiles();

        // ListView displaying all the files that fit the pattern bin\Debug\net10.01\*.csv
         filesView = new ListView () {
            X = 0,
            Y = 0,
            Width = Dim.Fill (),
            Height = Dim.Fill (),
            Text = fileList[0]
        };


        // // Create input components and labels
        // var usernameLabel = new Label () {
        //     Text = "Username:"
        // };

        // usernameText = new TextField ("") {
        //     // Position text field adjacent to the label
        //     X = Pos.Right (usernameLabel) + 1,

        //     // Fill remaining horizontal space
        //     Width = Dim.Fill (),
        // };

        // var passwordLabel = new Label () {
        //     Text = "Password:",
        //     X = Pos.Left (usernameLabel),
        //     Y = Pos.Bottom (usernameLabel) + 1
        // };

        // var passwordText = new TextField ("") {
        //     Secret = true,

        //     // align with the text box above
        //     X = Pos.Left (usernameText),
        //     Y = Pos.Top (passwordLabel),
        //     Width = Dim.Fill ()
        // };

        // // Create login button
        // var btnLogin = new Button () {
        //     Text = "Login",
        //     Y = Pos.Bottom (passwordLabel) + 1,

        //     // center the login button horizontally
        //     X = Pos.Center (),
        //     IsDefault = true
        // };

        // // When login button is clicked display a message popup
        // btnLogin.Clicked += () => {
        //     if (usernameText.Text == "admin" && passwordText.Text == "password") {
        //         MessageBox.Query ("Logging In", "Login Successful", "Ok");
        //         Username = usernameText.Text.ToString ();
        //         Application.RequestStop ();
        //     } else {
        //         MessageBox.ErrorQuery ("Logging In", "Incorrect username or password", "Ok");
        //     }
        // };

        // Add the views to the Window
        Add (filesView);
    }
}
