namespace LoE_Launcher.Utils;

public class GifBackgroundProvider
{
    public static View CreateAnimatedBackground(string gifUrl)
    {
#if MACCATALYST
        // For Mac, use a WebView approach
        return CreateWebViewBackground(gifUrl);
#else
            // For other platforms, use the standard Image control
            return new Image
            {
                Source = ImageSource.FromUri(new Uri(gifUrl)),
                Aspect = Aspect.AspectFill,
                HorizontalOptions = LayoutOptions.Fill,
                VerticalOptions = LayoutOptions.Fill
            };
#endif
    }

    private static WebView CreateWebViewBackground(string gifUrl)
    {
        var htmlContent =
            $$"""
              <!DOCTYPE html>
              <html>
              <head>
                  <style>
                      body, html {
                          margin: 0;
                          padding: 0;
                          height: 100%;
                          overflow: hidden;
                      }
                      .bg-container {
                          width: 100%;
                          height: 100%;
                          display: flex;
                          justify-content: center;
                          align-items: center;
                          overflow: hidden;
                          background-color: #121212;
                      }
                      .bg-image {
                          min-width: 100%;
                          min-height: 100%;
                          width: auto;
                          height: auto;
                          object-fit: cover;
                      }
                  </style>
              </head>
              <body>
                  <div class='bg-container'>
                      <img src='{{gifUrl}}' class='bg-image' />
                  </div>
              </body>
              </html>
              """;

        var webView = new WebView
        {
            Source = new HtmlWebViewSource
            {
                Html = htmlContent
            },
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            BackgroundColor = Colors.Transparent
        };

        return webView;
    }
}