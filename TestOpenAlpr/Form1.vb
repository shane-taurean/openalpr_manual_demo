Imports openalprnet
Imports System.IO
Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Linq

Public Class Form1
    Dim vehicleResults As List(Of VehicleResult)
    Dim images As List(Of Image)

    Private Sub Form1_Load(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles MyBase.Load
        OpenFileDialog1.InitialDirectory = My.Application.Info.DirectoryPath
        cmbMaxResults.SelectedIndex = 0
        cmbMaxVehicles.SelectedIndex = 0
    End Sub

    Private Sub Button1_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button1.Click
        StatusLabel1.Text = ""
        OpenFileDialog1.Multiselect = False
        OpenFileDialog1.Filter = "Image Files |*.JPG; *.JPEG; *.PNG; *.BMP|JPEG Image |*.JPG; *.JPEG|PNG Image |*.PNG|Bitmap |*.BMP"
        OpenFileDialog1.Title = "Select an Image"
        OpenFileDialog1.FileName = ""

        If OpenFileDialog1.ShowDialog() = Windows.Forms.DialogResult.OK Then
            lblCaution.Visible = False
            TextBox1.Text = OpenFileDialog1.FileName
            PictureBox1.Image = Image.FromFile(TextBox1.Text)
            processImage()
        End If
    End Sub

    Private Function recognizeNumber(ByVal path As String) As Boolean
        Dim foundBanned As Boolean = False
        Try
            Dim confPath As String = My.Application.Info.DirectoryPath & "\openalpr.conf"
            Dim runtimePath As String = My.Application.Info.DirectoryPath & "\runtime_data\"

            Dim alpr As AlprNet = New AlprNet("eu", confPath, runtimePath)

            If Not alpr.IsLoaded Then
                Throw New System.Exception("Error loading openalpr library")
            End If

            'alpr.DefaultRegion = "eu"
            alpr.DetectRegion = True
            alpr.TopN = Val(cmbMaxResults.Text)

            Dim results As AlprResultsNet = alpr.Recognize(path)

            If Not IsNothing(images) Then
                images = Nothing
            End If

            If Not IsNothing(vehicleResults) Then
                vehicleResults = Nothing
            End If

            images = New List(Of Image)(results.Plates.Count())
            vehicleResults = New List(Of VehicleResult)

            Dim count As Int16 = 1
            Dim listIndex As Int16 = 0

            If results.Plates.Count > 0 Then
                For Each result As AlprPlateResultNet In results.Plates
                    If count > Val(cmbMaxVehicles.Text) Then
                        Exit For
                    End If

                    Dim rect As Rectangle = boundingRectangle(result.PlatePoints)
                    Dim img As Image = Image.FromFile(path)
                    Dim cropped As Image = cropImage(img, rect)
                    cropped = resizeImage(cropped)
                    images.Add(cropped)

                    For Each plate As AlprPlateNet In result.TopNPlates
                        lstBoxVehicles.Items.Add(plate.Characters)
                        'lstBoxVehicles.SelectedIndex = 0

                        If Not foundBanned And vehContact.isBarred(plate.Characters) Then
                            foundBanned = True
                        End If

                        vehicleResults.Add(New VehicleResult(plate.Characters, listIndex, count - 1))

                        listIndex += 1
                    Next

                    count += 1
                Next

                If foundBanned Then
                    lblCaution.Visible = True
                End If

                Return True
            Else
                Return False
            End If
        Catch ex As Exception
            'MessageBox.Show(ex.Message)
        End Try

        Return False
    End Function

    Private Sub processImage()
        lstBoxVehicles.Items.Clear()
        lstBoxVehicles.Text = ""

        If File.Exists(TextBox1.Text) Then
            lblCaution.Visible = False
            lstBoxVehicles.Items.Clear()
            lstBoxVehicles.Text = ""
            PictureBox3.Image = Nothing
            StatusLabel1.Text = "Processing"

            If Not recognizeNumber(TextBox1.Text) Then
                StatusLabel1.Text = "-- FAILED! --"
                Button2.Enabled = False
                lstBoxVehicles.Items.Clear()
                lstBoxVehicles.Text = ""
                PictureBox3.Image = Nothing
            Else
                StatusLabel1.Text = "Done"
                Button2.Enabled = True
                lstBoxVehicles.SelectedIndex = 0
            End If
        End If
    End Sub

    Private Sub Button3_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button3.Click
        Form2.ShowDialog()
        StatusLabel1.Text = ""
    End Sub

    Private Sub cmb_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles cmbMaxVehicles.SelectedIndexChanged, cmbMaxResults.SelectedIndexChanged
        processImage()
    End Sub

    Public Function boundingRectangle(ByVal points As List(Of Point)) As Rectangle
        Dim minX As Integer = points.Min(Function(p) p.X)
        Dim minY As Integer = points.Min(Function(p) p.Y)
        Dim maxX As Integer = points.Max(Function(p) p.X)
        Dim maxY As Integer = points.Max(Function(p) p.Y)
        Return New Rectangle(New Point(minX, minY), New Size(maxX - minX, maxY - minY))
    End Function

    Private Function cropImage(ByVal img As Image, ByVal cropArea As Rectangle) As Image
        Dim bmpImage As Bitmap = New Bitmap(img)
        Return bmpImage.Clone(cropArea, bmpImage.PixelFormat)
    End Function

    Public Function combineImages(ByVal images As List(Of Image)) As Bitmap
        'read all images into memory
        Dim finalImage As Bitmap = Nothing

        Try
            Dim width As Integer = 0
            Dim height As Integer = 0

            For Each bmp As Image In images
                width += bmp.Width
                If bmp.Height > height Then
                    height = bmp.Height
                End If
            Next

            'create a bitmap to hold the combined image
            finalImage = New Bitmap(width, height)

            'get a graphics object from the image so we can draw on it
            Using g As Graphics = Graphics.FromImage(finalImage)
                'set background color
                g.Clear(Color.Black)

                'go through each image and draw it on the final image
                Dim offset As Integer = 0

                For Each Image As Bitmap In images
                    g.DrawImage(Image, New Rectangle(offset, 0, Image.Width, Image.Height))
                    offset += Image.Width
                Next

            End Using

            Return finalImage
        Catch ex As Exception
            If Not IsNothing(finalImage) Then finalImage.Dispose()

            Throw ex
        Finally
            For Each image As Image In images
                image.Dispose()
            Next
        End Try
    End Function

    Public Function resizeImage(ByVal image As Image) As Bitmap
        Dim BOXHEIGHT As Int16 = 30
        Dim BOXWIDTH As Int16 = 135

        Dim scaleHeight As Double = CDbl(BOXHEIGHT) / CDbl(image.Height)
        Dim scaleWidth As Double = CDbl(BOXWIDTH) / CDbl(image.Width)

        Dim scale As Double = Math.Min(scaleHeight, scaleWidth)

        Dim resizedImage As Bitmap = New Bitmap(image, CInt(image.Width * scale), CInt(image.Height * scale))

        Return resizedImage
    End Function

    Private Sub Button2_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles Button2.Click
        If Not IsNothing(PictureBox3.Image) Then
            Dim fileName As String = lstBoxVehicles.SelectedItem.ToString
            Dim fileExtenstion As String = ""
            Dim fileType As ImageFormat = Nothing
            SaveFileDialog1.Filter = "Image Files |*.JPG; *.JPEG; *.PNG; *.BMP|JPEG Image |*.JPG; *.JPEG|PNG Image |*.PNG|Bitmap |*.BMP"
            SaveFileDialog1.Title = "Save Image"
            SaveFileDialog1.DefaultExt = "jpg"
            SaveFileDialog1.FileName = fileName

            If SaveFileDialog1.ShowDialog = DialogResult.OK Then
                fileName = SaveFileDialog1.FileName

                If fileName <> "" Then
                    fileExtenstion = Path.GetExtension(fileName).ToLower
                    If fileExtenstion = ".jpg" Or fileExtenstion = ".jpeg" Then
                        fileType = ImageFormat.Jpeg
                    ElseIf fileExtenstion = ".bmp" Then
                        fileType = ImageFormat.Bmp
                    ElseIf fileExtenstion = ".png" Then
                        fileType = ImageFormat.Png
                    End If

                    If Not IsNothing(fileType) Then
                        PictureBox3.Image.Save(fileName, fileType)
                    End If
                End If
            End If
        End If
    End Sub

    Private Sub lstBoxVehicles_SelectedIndexChanged(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles lstBoxVehicles.SelectedIndexChanged
        If vehicleResults.Any Then
            Dim vehResult As VehicleResult = vehicleResults.Find(Function(x) x.listID = lstBoxVehicles.SelectedIndex)
            PictureBox3.Image = images(vehResult.imageID)
        End If
    End Sub


End Class
